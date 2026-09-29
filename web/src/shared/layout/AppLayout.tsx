import { Alert, AppShell, Avatar, Burger, Group, Image, Menu, NavLink, Text, Title, UnstyledButton } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { NavLink as RouterNavLink, Outlet } from 'react-router';
import { ApiError } from '../api/client';
import { useSession } from '../auth/session';
import { useConfig } from '../config';
import { useMe } from '../me';

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('');
}

export function AppLayout() {
  const config = useConfig();
  const session = useSession();
  const me = useMe();
  const [opened, { toggle, close }] = useDisclosure();
  const noGroup = me.error instanceof ApiError && me.error.status === 403;

  return (
    <AppShell header={{ height: 56 }} navbar={{ width: 220, breakpoint: 'sm', collapsed: { mobile: !opened } }} padding="md">
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group gap="sm">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Toggle navigation" />
            {config.logoUrl && <Image src={config.logoUrl} alt="" h={28} w="auto" />}
            <Title order={4}>{config.productName}</Title>
          </Group>
          {me.data && (
            <Menu position="bottom-end">
              <Menu.Target>
                <UnstyledButton aria-label="Account menu">
                  <Group gap="xs">
                    <Avatar size="sm" radius="xl">
                      {initials(me.data.name)}
                    </Avatar>
                    <Text size="sm">{me.data.name}</Text>
                  </Group>
                </UnstyledButton>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>{me.data.roles.join(', ')}</Menu.Label>
                <Menu.Item onClick={session.signOut}>Sign out</Menu.Item>
              </Menu.Dropdown>
            </Menu>
          )}
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="xs">
        <NavLink component={RouterNavLink} to="/projects" label="Projects" onClick={close} />
      </AppShell.Navbar>
      <AppShell.Main>
        {noGroup ? (
          <Alert color="yellow" title="No access yet">
            Your account isn't in a PDS group. Ask an administrator to add you to Viewer, Manager or Admin.
          </Alert>
        ) : (
          <Outlet />
        )}
      </AppShell.Main>
    </AppShell>
  );
}
