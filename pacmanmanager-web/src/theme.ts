import { createTheme } from "@mui/material/styles";

/**
 * The app's one MUI theme. Colours are emitted as CSS variables so that a `dark` scheme can be
 * added alongside `light` without restyling anything.
 */
export const theme = createTheme({
  cssVariables: true,
  colorSchemes: { light: true },
});
