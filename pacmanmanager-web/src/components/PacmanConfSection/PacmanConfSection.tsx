import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";
import Accordion from "@mui/material/Accordion";
import AccordionDetails from "@mui/material/AccordionDetails";
import AccordionSummary, {
  accordionSummaryClasses,
} from "@mui/material/AccordionSummary";
import Alert from "@mui/material/Alert";
import Typography from "@mui/material/Typography";
import { useId } from "react";
import type { Repository } from "../../api";
import { CopyableCodeBlock } from "../CopyableCodeBlock";
import { pacmanConfSectionText } from "./pacmanConfSectionText";

/** The props {@link PacmanConfSection} takes. */
export interface PacmanConfSectionProps {
  /** The repository whose `pacman.conf` section is shown, as the API returns it. */
  repository: Repository;
}

/** The instruction shown beside **Copy**. */
export const instructionText =
  "Add this section to /etc/pacman.conf, then run pacman -Sy.";

/** The note shown below the section for a private repository. */
export const privateNoteText =
  "This repository is private: pacman must authenticate to read it, so the Server line needs credentials.";

/** Sets the title and its secondary text on one baseline, as the wireframes show them. */
const summarySx = {
  [`& .${accordionSummaryClasses.content}`]: { alignItems: "baseline" },
};

/**
 * A **Use this repository** accordion, collapsed each time it is rendered, holding the
 * `pacman.conf` section that adds the repository to pacman in a {@link CopyableCodeBlock}. For a
 * private repository a note below the section says that pacman must authenticate to read it.
 */
export function PacmanConfSection({ repository }: PacmanConfSectionProps) {
  const id = useId();
  const summaryId = `${id}-summary`;
  const detailsId = `${id}-details`;

  return (
    <Accordion variant="outlined" disableGutters>
      <AccordionSummary
        id={summaryId}
        aria-controls={detailsId}
        expandIcon={<ArrowDropDownIcon />}
        sx={summarySx}
      >
        <Typography component="span" sx={{ mr: 2 }}>
          Use this repository
        </Typography>
        <Typography component="span" variant="body2" color="text.secondary">
          The pacman.conf section for this repository
        </Typography>
      </AccordionSummary>
      <AccordionDetails>
        <CopyableCodeBlock
          text={pacmanConfSectionText(repository)}
          description={instructionText}
        />
        {!repository.isPublic && (
          <Alert severity="info" sx={{ mt: 1.5 }}>
            {privateNoteText}
          </Alert>
        )}
      </AccordionDetails>
    </Accordion>
  );
}
