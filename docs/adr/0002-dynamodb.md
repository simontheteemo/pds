# ADR-0002: DynamoDB instead of Aurora PostgreSQL + EF Core

- Status: **Accepted** (2026-09-29, @simon)
- Date: 2026-09-29

## Context

The first draft of the foundation spec used Aurora Serverless v2 PostgreSQL with EF Core, with one schema per module. Drawing the runtime topology (architecture set, sheet A-01) showed how much infrastructure a relational database adds on Lambda:

- a VPC with private subnets
- a Secrets Manager VPC endpoint
- a migrator Lambda run as a CDK Trigger
- a reserved-concurrency cap to protect database connections
- a minimum Aurora capacity in prod, about US$70–90/month

The team's guiding principle is lightweight frameworks and provider-managed infrastructure.

## Decision

Use **Amazon DynamoDB**, with **one table per module**, on-demand capacity, and point-in-time recovery.

- Data access uses the AWS SDK low-level client (`IAmazonDynamoDB`), with a small hand-written item mapper per module. No ORM.
- Every table uses a generic `pk` / `sk` key schema, plus a `gsi1` index (`gsi1pk` / `gsi1sk`) for listing.
- Optimistic concurrency uses a numeric `version` attribute and condition expressions.
- Uniqueness (such as project code) uses guard items written in the same `TransactWriteItems` call.
- Aggregates such as budget vs actual are kept as running-total items, updated in the same transaction as the source record.
- Ad-hoc reporting (later) uses DynamoDB export to S3, queried with Athena.

## Consequences

**Gains**
- There's no VPC, no NAT or endpoints, no connection pooling, no migrations and no migrator.
- The Lambda runs outside a VPC.
- Idle cost is close to zero, and prod run cost is a few US$/month at the expected load.
- Module isolation is physical: a module's IAM access and its table are the same boundary.

**Costs**
- Access patterns must be designed up front, and a new query shape may need a new GSI or a backfill.
- There's no ad-hoc SQL. Reports are either pre-computed (running totals) or run on the S3 export with Athena.
- List filtering and search for Portfolio happen in memory over the project set. That's fine up to a few thousand projects; this is the revisit point.
- Transactions are limited to 100 items. Bulk operations must be batched and idempotent.
- There are no schema migrations. When the data shape changes, the code reads both old and new shapes, and a one-off backfill script updates old items if needed.

## Revisit when

- Reporting needs outgrow running totals and Athena (for example, interactive multi-dimensional analysis). Then consider streaming into a relational or analytical store.
- The number of projects per deployment grows past a few thousand. Then add GSIs per filter, or a search index.
