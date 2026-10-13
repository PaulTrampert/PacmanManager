import { describe, expect, it } from "vitest";
import { translatePattern } from "./translatePattern";

describe("translatePattern", () => {
  describe("each recognised construct translates exactly", () => {
    it.each([
      // \A and \z become JavaScript's anchors.
      ["\\Aabc", "^abc"],
      ["abc\\z", "abc$"],
      ["\\Aabc\\z", "^abc$"],
      // ^ without the Multiline option.
      ["^abc", "^abc"],
      // Literal characters, and escaped metacharacters.
      ["abc", "abc"],
      ["a@_ #/", "a@_ #/"],
      [
        "\\.\\+\\*\\?\\(\\)\\[\\]\\{\\}\\|\\^\\$\\\\\\/",
        "\\.\\+\\*\\?\\(\\)\\[\\]\\{\\}\\|\\^\\$\\\\\\/",
      ],
      // Character classes of literals, escaped metacharacters and ranges, negated or not.
      ["[abc]", "[abc]"],
      ["[^abc]", "[^abc]"],
      ["[a-zA-Z0-9]", "[a-zA-Z0-9]"],
      ["[^a-z]", "[^a-z]"],
      ["[@_\\+\\.\\-\\]\\\\]", "[@_\\+\\.\\-\\]\\\\]"],
      ["[-a]", "[-a]"],
      ["[a-]", "[a-]"],
      ["[.$^(|]", "[.$^(|]"],
      ["[\\+-\\.]", "[\\+-\\.]"],
      // Groups and alternation.
      ["(ab)", "(ab)"],
      ["(?:ab)", "(?:ab)"],
      ["a|b", "a|b"],
      ["(a|(?:b|c))", "(a|(?:b|c))"],
      // Quantifiers, greedy and lazy.
      ["a*", "a*"],
      ["a+", "a+"],
      ["a?", "a?"],
      ["a{2}", "a{2}"],
      ["a{2,}", "a{2,}"],
      ["a{2,5}", "a{2,5}"],
      ["a*?", "a*?"],
      ["a+?", "a+?"],
      ["a??", "a??"],
      ["a{2}?", "a{2}?"],
      ["a{2,}?", "a{2,}?"],
      ["a{2,5}?", "a{2,5}?"],
      ["[a-z]+", "[a-z]+"],
      ["(ab)*", "(ab)*"],
      ["\\.+", "\\.+"],
    ])("%s becomes %s", (pattern, source) => {
      expect(translatePattern(pattern)).toEqual({ kind: "exact", source });
    });

    it("translates RepoHost's repository-name pattern", () => {
      expect(
        translatePattern("\\A[a-zA-Z0-9@_\\+]([-\\.]?[a-zA-Z0-9@_\\+])*\\z"),
      ).toEqual({
        kind: "exact",
        source: "^[a-zA-Z0-9@_\\+]([-\\.]?[a-zA-Z0-9@_\\+])*$",
      });
    });

    it("produces a source that compiles under the u flag", () => {
      const result = translatePattern("\\A[a-z]([-\\.]?[a-z0-9])*\\z");

      expect(result.kind).toBe("exact");
      if (result.kind === "exact") {
        expect(() => new RegExp(result.source, "u")).not.toThrow();
      }
    });
  });

  describe("any other construct makes the pattern server-only, naming it", () => {
    it.each([
      // The constructs whose meaning differs between the engines.
      ["\\A\\d+\\z", "\\d"],
      ["\\A\\w+\\z", "\\w"],
      ["\\A\\s+\\z", "\\s"],
      ["\\Aa\\b", "\\b"],
      ["\\Aa.b\\z", "."],
      ["^abc$", "$"],
      ["\\Aabc\\Z", "\\Z"],
      ["(?i)abc", "(?i)"],
      // The same escapes inside a character class.
      ["\\A[\\w-]+\\z", "\\w"],
      ["[\\d]", "\\d"],
      ["[a\\s]", "\\s"],
      ["[\\b]", "\\b"],
      ["[\\Z]", "\\Z"],
      // The rest of what the translator does not know.
      ["\\Gabc", "\\G"],
      ["(?i:abc)", "(?i:"],
      ["(?m)^abc", "(?m)"],
      ["(?>abc)", "(?>"],
      ["(?(a)b|c)", "(?(a)"],
      ["(?=a)b", "(?=a)"],
      ["(?<name>a)", "(?<name>"],
      ["a++", "+"],
      ["a*+", "+"],
      ["\\p{L}", "\\p"],
      ["[\\p{L}]", "\\p"],
      ["(a)\\1", "\\1"],
      ["\\n", "\\n"],
      ["\\-", "\\-"],
      ["\\@", "\\@"],
      ["[a-z-[aeiou]]", "-["],
      ["[a[b]", "["],
      ["[a-z-0]", "-"],
      ["[]a]", "[]"],
      ["[^]a]", "[^]"],
      ["*a", "*"],
      ["^*", "*"],
      ["a|*", "*"],
      ["(*a)", "*"],
      ["a{,2}", "{"],
      ["a{", "{"],
      ["a}", "}"],
      ["a]", "]"],
      ["(ab", "("],
      ["ab)", ")"],
      ["[ab", "["],
      ["ab\\", "\\"],
      ["\u{1F600}+", "\uD83D"],
    ])("%s names %s", (pattern, construct) => {
      expect(translatePattern(pattern)).toEqual({
        kind: "server-only",
        construct,
      });
    });

    it("names the whole pattern when its tokens are recognised but it does not compile", () => {
      expect(translatePattern("[z-a]")).toEqual({
        kind: "server-only",
        construct: "[z-a]",
      });
    });
  });
});
