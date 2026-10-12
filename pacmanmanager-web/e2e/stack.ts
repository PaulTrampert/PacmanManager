// Where the compose stack the end-to-end tests run against answers. See compose.yaml and
// docs/web-ui-architecture/web-ui-architecture.md, "End-to-end tests".

/** The web host (SpaHost serving the built app), which every test's page talks to. */
export const webHostUrl = "http://localhost:8081";

/** Keycloak, as the browser and the tests reach it. */
export const keycloakUrl = "http://localhost:8080";

/** The Keycloak realm the web host signs users in against. */
export const realm = "localdev";

/** The command that brings the stack up; `npm run compose-up` runs it before `npm run e2e`. */
export const composeUpCommand = "docker compose up -d --build";

/** The addresses that must answer before any test runs: the web host and the realm's discovery document. */
export const readinessUrls = [
  `${webHostUrl}/`,
  `${keycloakUrl}/realms/${realm}/.well-known/openid-configuration`,
] as const;
