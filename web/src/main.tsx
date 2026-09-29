import '@mantine/core/styles.css';
import '@mantine/notifications/styles.css';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { loadConfig } from './shared/config';

const root = document.getElementById('root');
if (!root) throw new Error('Missing #root element');

loadConfig()
  .then((config) => {
    document.title = config.productName;
    createRoot(root).render(
      <StrictMode>
        <App config={config} />
      </StrictMode>,
    );
  })
  .catch((error: unknown) => {
    root.textContent = `Could not load configuration: ${error instanceof Error ? error.message : String(error)}`;
  });
