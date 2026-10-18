# PDS readiness review: workflow gaps

- Date: 2026-10-18
- Author: Claude, at Simon's request: "the management module needs some practical enhancement … this system is not ready for use"
- Method: walk through what each role actually does in a normal week. For each step, check whether PDS supports it today.

## Summary

Every module can store its own records: projects, risks, contractors and contracts, cost lines, consents, units, documents. What PDS lacks is the layer people use to **run** projects from day to day:
- what's due and who owns it
- the project's key dates and whether they're slipping
- what needs attention right now
- the paperwork that comes in, such as invoices

The project detail page is one long stack of module sections, and the app opens on a plain project list. Nobody can use PDS to answer "what do I need to do today?"

## Workflows walked through

| Role | Weekly job | Supported today? | Gap |
|---|---|---|---|
| Project manager | Check the programme: are key dates (consent lodged and granted, construction start, practical completion, CCC, titles, settlements) on track? | ❌ | The project has only planned and actual start and completion dates. There are no milestones, no forecast dates and no slippage. |
| Project manager | Chase actions from site and consultant meetings: who owes what, by when | ❌ | There are no tasks or actions, no owners, and no "my actions" view. |
| Project manager | Know the project team: internal PM and development manager; external architect, engineer, QS, lawyer, council officer | ⚠️ | `ProjectManager` is free text. Consultants exist as contractors, but nothing records who plays which role on which project. |
| Development manager | Open the app and see what needs attention across all projects | ❌ | There is no home dashboard. Overdue consents are on a separate page; high risks, overdue actions and slipping milestones are visible nowhere. |
| Development manager | Monthly project status to the directors: RAG, commentary, key issues, next steps | ❌ | `Status` is one enum with no commentary, history or report. |
| Development manager | Portfolio overview: budget vs forecast, sales position and stage pipeline across projects | ⚠️ | There is a Sales summary across projects, but no cost or overall portfolio dashboard. |
| Accountant / PM | Receive a contractor invoice, attach the PDF, approve it, mark it paid, and see everything a contractor has invoiced | ❌ | Being fixed in **PR: contractor invoices** (`feat/contractor-invoices`). |
| Anyone | Find the project's information quickly | ⚠️ | The project page is one long scroll and needs a workspace with tabs. |
| Admin | Load the client's existing projects, contractors and units on day one | ❌ | There is no import, so every record has to be typed in by hand. |

## Prioritised plan

### P0: needed before real use

1. **Contractor invoices and attachments.** In progress on `feat/contractor-invoices`.
2. **Project workspace and programme** (Portfolio):
   - A project page with tabs: Overview · Programme · Costs · Contracts · Consents · Sales · Documents · Risks.
   - An Overview tab with key facts, the next milestones, and a "needs attention" panel.
   - **Milestones:** typed key dates with planned, forecast and actual dates, slippage in days, and a status. The standard NZ development set is offered per project and stays configurable.
   - Portfolio overview totals.
3. **Actions and people, plus a home dashboard:**
   - Actions per project, with owner, due date, status and an optional link to a risk, consent, contract or milestone. Includes a "My actions" view.
   - A people directory built from the Cognito users, so owners and the project team are real users.
   - A **Home** page as the landing screen: my actions, overdue consent items, high risks and slipping milestones, across projects.

### P1: soon after go-live

4. **Monthly project status reports.** A RAG rating per area, commentary, issues and next steps, with a dated history and an export.
5. **Meetings and decisions log.** Meeting notes per project; actions raised in a meeting flow into Actions.
6. **CSV import** for projects, contractors and units, for onboarding.

### P2: later

7. A shared reminder digest covering actions and milestones as well as consents. Global search. In-app user invites and role management, which today happens in the Cognito console. Activity history per record.

## Build approach

- **P0 items 2 and 3** run in parallel worktrees, alongside the invoices branch.
- Each item goes through spec → plan → SDD build with reviews, and is merged by the coordinator.
- **Shared touch point:** item 2 owns the restructure of `ProjectDetailPage`. Item 3 adds its panels through the Overview tab's extension points, or through a follow-up after both have merged.
