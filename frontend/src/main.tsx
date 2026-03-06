import React from "react";
import ReactDOM from "react-dom/client";
import { InteractionRequiredAuthError, PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
import { configureApiAuth } from "./api";
import { App } from "./App";
import "./styles.css";

const entraClientId = import.meta.env.VITE_ENTRA_CLIENT_ID?.trim();
const entraAuthority = import.meta.env.VITE_ENTRA_AUTHORITY?.trim() ?? "https://login.microsoftonline.com/common";
const apiScopes = (import.meta.env.VITE_API_SCOPES ?? "")
  .split(",")
  .map((scope: string) => scope.trim())
  .filter((scope: string) => scope.length > 0);

const authConfigured = Boolean(entraClientId) && apiScopes.length > 0;

const msalInstance = authConfigured
  ? new PublicClientApplication({
      auth: {
        clientId: entraClientId!,
        authority: entraAuthority,
        redirectUri: window.location.origin
      }
    })
  : null;

configureApiAuth(async () => {
  if (!authConfigured || !msalInstance) {
    return null;
  }

  let account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0] ?? null;

  if (!account) {
    const loginResult = await msalInstance.loginPopup({ scopes: apiScopes });
    account = loginResult.account;
  }

  if (!account) {
    return null;
  }

  msalInstance.setActiveAccount(account);

  try {
    const tokenResult = await msalInstance.acquireTokenSilent({ scopes: apiScopes, account });
    return tokenResult.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      const interactiveResult = await msalInstance.acquireTokenPopup({ scopes: apiScopes });
      return interactiveResult.accessToken;
    }

    throw error;
  }
});

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {msalInstance ? (
      <MsalProvider instance={msalInstance}>
        <App />
      </MsalProvider>
    ) : (
      <App />
    )}
  </React.StrictMode>
);
