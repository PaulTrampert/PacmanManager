// Translates a .NET regular expression, as Swashbuckle copies a `[RegularExpression]` into the OpenAPI
// document, into a JavaScript one that means exactly the same thing, or reports that it cannot. Used
// by scripts/generate-validation.ts; see docs/web-ui-repositories/web-ui-repositories.md, "Validation
// rules from the OpenAPI document".
//
// It tokenises the pattern and recognises only constructs that mean the same in both engines. Anything
// else makes the whole pattern server-only, never passed through untranslated: a browser check that
// is stricter than the API would block input the API accepts, while one that is looser costs only a
// round trip.

/** The outcome of translating a pattern. */
export type PatternTranslation =
  | {
      /** Every construct was recognised; `source` means the same in JavaScript under the `u` flag. */
      kind: "exact";
      source: string;
    }
  | {
      /** Some construct was not recognised, so only the server can check the pattern. */
      kind: "server-only";
      /** The first construct the translator did not recognise. */
      construct: string;
    };

/** Characters JavaScript lets a `u`-flag pattern escape outside a character class. */
const syntaxCharacters = new Set("^$\\.*+?()[]{}|/");

/** Characters JavaScript lets a `u`-flag pattern escape inside a character class. */
const classSyntaxCharacters = new Set([...syntaxCharacters, "-"]);

const boundedQuantifier = /^\{\d+(?:,\d*)?\}/;

class Unrecognised extends Error {
  readonly construct: string;

  constructor(construct: string) {
    super(`Unrecognised construct ${construct}`);
    this.construct = construct;
  }
}

function isSurrogate(character: string): boolean {
  const code = character.charCodeAt(0);
  return code >= 0xd800 && code <= 0xdfff;
}

/**
 * Translates the .NET regular expression `pattern` into JavaScript, or names the first construct in
 * it that the translator does not recognise as meaning the same in both.
 */
export function translatePattern(pattern: string): PatternTranslation {
  let source: string;
  try {
    source = new Translator(pattern).translate();
  } catch (error) {
    if (error instanceof Unrecognised) {
      return { kind: "server-only", construct: error.construct };
    }
    throw error;
  }

  try {
    new RegExp(source, "u");
  } catch {
    // Every token was recognised, yet the whole does not compile (a range running backwards, say).
    return { kind: "server-only", construct: pattern };
  }
  return { kind: "exact", source };
}

class Translator {
  private readonly pattern: string;
  private index = 0;
  private output = "";
  /** Whether the last token is one a quantifier may follow. */
  private quantifiable = false;
  private openGroups = 0;

  constructor(pattern: string) {
    this.pattern = pattern;
  }

  translate(): string {
    while (this.index < this.pattern.length) {
      this.token();
    }
    if (this.openGroups > 0) {
      throw new Unrecognised("(");
    }
    return this.output;
  }

  private token(): void {
    const character = this.pattern[this.index];
    switch (character) {
      case "\\":
        this.escape();
        return;
      case "^":
        // Recognised because no inline option can turn on Multiline: those are unrecognised.
        this.emit("^", false);
        return;
      case "[":
        this.characterClass();
        return;
      case "(":
        this.openGroup();
        return;
      case ")":
        if (this.openGroups === 0) {
          throw new Unrecognised(")");
        }
        this.openGroups--;
        this.emit(")", true);
        return;
      case "|":
        this.emit("|", false);
        return;
      case "*":
      case "+":
      case "?":
        this.quantifier(character);
        return;
      case "{": {
        const bounded = boundedQuantifier.exec(this.pattern.slice(this.index));
        if (!bounded) {
          throw new Unrecognised("{");
        }
        this.quantifier(bounded[0]);
        return;
      }
      // `.` and a bare `$` differ between the engines; a lone `]` or `}` is a literal in .NET and an
      // error in JavaScript under the `u` flag.
      case ".":
      case "$":
      case "]":
      case "}":
        throw new Unrecognised(character);
      default:
        // A surrogate is one UTF-16 unit to .NET and half a code point to JavaScript.
        if (isSurrogate(character)) {
          throw new Unrecognised(character);
        }
        this.emit(character, true);
    }
  }

  private emit(text: string, quantifiable: boolean): void {
    this.output += text;
    this.index += text.length;
    this.quantifiable = quantifiable;
  }

  private escape(): void {
    const escaped = this.pattern[this.index + 1];
    if (escaped === undefined) {
      throw new Unrecognised("\\");
    }
    if (escaped === "A") {
      this.output += "^";
      this.index += 2;
      this.quantifiable = false;
    } else if (escaped === "z") {
      this.output += "$";
      this.index += 2;
      this.quantifiable = false;
    } else if (syntaxCharacters.has(escaped)) {
      this.emit(`\\${escaped}`, true);
    } else {
      throw new Unrecognised(`\\${escaped}`);
    }
  }

  private openGroup(): void {
    if (this.pattern[this.index + 1] === "?") {
      if (this.pattern[this.index + 2] !== ":") {
        // Inline options, lookarounds, named and atomic groups, conditionals, comments: name the
        // construct up to the `)` or `:` that ends its introduction, as in `(?i)` or `(?i:`.
        let end = this.index + 2;
        while (
          end < this.pattern.length &&
          !")>:".includes(this.pattern[end])
        ) {
          end++;
        }
        throw new Unrecognised(this.pattern.slice(this.index, end + 1));
      }
      this.emit("(?:", false);
    } else {
      this.emit("(", false);
    }
    this.openGroups++;
  }

  private quantifier(quantifier: string): void {
    if (!this.quantifiable) {
      // At the start, after `(`, `|`, an anchor, or another quantifier (a possessive one included).
      throw new Unrecognised(quantifier);
    }
    this.emit(quantifier, false);
    if (this.pattern[this.index] === "?") {
      this.emit("?", false);
    }
  }

  private characterClass(): void {
    const start = this.index;
    let text = "[";
    let position = start + 1;
    if (this.pattern[position] === "^") {
      text += "^";
      position++;
    }
    // `[]…]` and `[^]…]` open with a literal `]` in .NET, and are an empty class in JavaScript.
    if (this.pattern[position] === "]") {
      throw new Unrecognised(this.pattern.slice(start, position + 1));
    }

    const atom = (): string => {
      const character = this.pattern[position];
      if (character === undefined) {
        throw new Unrecognised("[");
      }
      if (character === "\\") {
        const escaped = this.pattern[position + 1];
        if (escaped === undefined || !classSyntaxCharacters.has(escaped)) {
          // `\w`, `\d`, `\s`, `\b` (a backspace here), `\p{…}` and the rest.
          throw new Unrecognised(`\\${escaped ?? ""}`);
        }
        position += 2;
        return `\\${escaped}`;
      }
      // A nested `[` is literal in .NET and in JavaScript's `u` mode, but set notation under `v`.
      if (character === "[" || isSurrogate(character)) {
        throw new Unrecognised(character);
      }
      position++;
      return character;
    };

    let first = true;
    for (;;) {
      const character = this.pattern[position];
      if (character === undefined) {
        throw new Unrecognised("[");
      }
      if (character === "]") {
        break;
      }
      if (character === "-") {
        // .NET's class subtraction, `[a-z-[aeiou]]`.
        if (this.pattern[position + 1] === "[") {
          throw new Unrecognised("-[");
        }
        // A `-` is a literal only first or last; anywhere else outside a range the engines disagree.
        if (!first && this.pattern[position + 1] !== "]") {
          throw new Unrecognised("-");
        }
        text += "-";
        position++;
        first = false;
        continue;
      }

      text += atom();
      if (
        this.pattern[position] === "-" &&
        this.pattern[position + 1] !== undefined &&
        this.pattern[position + 1] !== "]"
      ) {
        position++;
        if (this.pattern[position] === "-") {
          throw new Unrecognised("-");
        }
        text += `-${atom()}`;
      }
      first = false;
    }

    text += "]";
    this.output += text;
    this.index = position + 1;
    this.quantifiable = true;
  }
}
