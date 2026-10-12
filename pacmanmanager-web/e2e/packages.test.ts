import { gunzipSync } from "node:zlib";
import { describe, expect, it } from "vitest";
import { buildPackage } from "./packages.ts";

interface ReadEntry {
  path: string;
  type: string;
  contents: Buffer;
  checksumMatches: boolean;
}

/** Reads the entries of an uncompressed ustar archive, checking each header's checksum. */
function readTar(archive: Buffer): ReadEntry[] {
  const entries: ReadEntry[] = [];
  let offset = 0;
  while (offset + 512 <= archive.length) {
    const header = archive.subarray(offset, offset + 512);
    if (header.every((byte) => byte === 0)) {
      break;
    }
    const field = (start: number, length: number) =>
      header
        .subarray(start, start + length)
        .toString("ascii")
        .replace(/\0.*$/s, "");
    const size = parseInt(field(124, 12), 8);
    const recorded = parseInt(field(148, 8).trim(), 8);
    const actual =
      header.reduce((sum, byte) => sum + byte, 0) -
      header.subarray(148, 156).reduce((sum, byte) => sum + byte, 0) +
      8 * 0x20;
    entries.push({
      path: field(0, 100),
      type: field(156, 1),
      contents: archive.subarray(offset + 512, offset + 512 + size),
      checksumMatches: recorded === actual,
    });
    offset += 512 + Math.ceil(size / 512) * 512;
  }
  return entries;
}

describe("buildPackage", () => {
  const spec = { name: "e2e-package", version: "1.2.3-4", payloadBytes: 1000 };

  it("writes a gzip-compressed file", () => {
    const pkg = buildPackage(spec);

    expect([...pkg.subarray(0, 2)]).toEqual([0x1f, 0x8b]);
  });

  it("starts the archive with a .PKGINFO naming the package", () => {
    const entries = readTar(gunzipSync(buildPackage(spec)));

    expect(entries[0]?.path).toBe(".PKGINFO");
    const pkginfo = entries[0]?.contents.toString("utf8") ?? "";
    expect(pkginfo).toMatch(/^pkgname = e2e-package$/m);
    expect(pkginfo).toMatch(/^pkgver = 1\.2\.3-4$/m);
    expect(pkginfo).toMatch(/^arch = x86_64$/m);
    expect(pkginfo).toMatch(/^size = 1000$/m);
  });

  it("uses the architecture it is given", () => {
    const entries = readTar(gunzipSync(buildPackage({ ...spec, arch: "any" })));

    expect(entries[0]?.contents.toString("utf8")).toMatch(/^arch = any$/m);
  });

  it("carries a payload file of the requested size", () => {
    const entries = readTar(gunzipSync(buildPackage(spec)));

    const payload = entries.find(
      (entry) => entry.path === "usr/share/e2e-package/payload",
    );
    expect(payload?.type).toBe("0");
    expect(payload?.contents.length).toBe(1000);
  });

  it("writes headers whose checksums are correct", () => {
    const entries = readTar(gunzipSync(buildPackage(spec)));

    expect(entries.length).toBeGreaterThan(1);
    expect(entries.every((entry) => entry.checksumMatches)).toBe(true);
  });

  it("is at least as large as its payload, which does not compress", () => {
    const pkg = buildPackage({ ...spec, payloadBytes: 64 * 1024 });

    expect(pkg.length).toBeGreaterThan(64 * 1024);
  });
});
