/**
 * A date-time from the API as a medium-length date in the reader's locale and time zone, such as
 * "Mar 2, 2026". `locales` is for tests; the app leaves it to the browser.
 */
export function formatDate(isoDateTime: string, locales?: string): string {
  return new Intl.DateTimeFormat(locales, { dateStyle: "medium" }).format(
    new Date(isoDateTime),
  );
}
