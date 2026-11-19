# The AstroCast website

This folder is the source of the AstroCast website, served at
<https://www.sthornhill.com/AstroCast/>. It is a static site: HTML, one stylesheet, fonts,
images and two PDFs. There is no PHP, no JavaScript, no database and no `data` folder.

| Path | What it is |
|---|---|
| `AstroCast/` | **Exactly what is uploaded** to the server folder `/public/AstroCast`. Nothing that is not meant to be served goes inside it |
| `Update-WebsiteRelease.ps1` | The per-release step: writes a release's version, file names, sizes, addresses and checksums into the pages |
| `samples-src/` | The Markdown sources of the sample pages, as they were written (one is an excerpt and two are transcriptions: see "The samples"). Not uploaded |
| `tools/Build-Samples.ps1` | Renders the sample pages from `samples-src/`. Not uploaded |
| `tools/og-card.html` | The source of the social card image `AstroCast/images/og-card-v1.png`. Not uploaded |
| `README.md` | This file. Not uploaded |

## How the site is laid out

```
AstroCast/
  index.html         the main page, self-contained: its CSS is inline in a <style> block
  help-v1.css        the one stylesheet the six help pages share
  help/index.html    the help index
  help/install-windows/  install-mac/  first-reading/  map-key/  faq/   one index.html each
  samples-v1.css     the one stylesheet the sample pages share
  samples/index.html the list of samples
  samples/<name>/    one index.html for each sample report or reading (built, not hand-written)
  samples/files/     the samples' PDF and Markdown downloads (versioned names)
  docs/              the two guides as PDFs (versioned names)
  fonts/             Source Sans 3 (400, 600), JetBrains Mono (400), their licences, PROVENANCE.txt
  images/            screenshots as PNG and WebP pairs, the icons and the social card
  favicon.ico        16, 32 and 48 px
  .htaccess          response headers for this folder only
  sitemap.txt        the eighteen listed pages (every page but the unlisted horary one)
  llms.txt           a plain-text summary for language-model crawlers
```

`index.html` runs in this order: the header band; the two Chart-panel screenshots; the download
card; "Get started in three steps"; "What it does" (ten cards, one per panel); "AstroCast in
use" (six screenshots); "Documentation" (the two guides); "See what you get" (the samples); the
prompt library; the map's background; system requirements; checking your download; common
questions; the footer.

Rules the pages keep:

- **Nothing is loaded from another host.** No external script, stylesheet, font or image. The
  only outside addresses are links to the AstroCast repository on GitHub and to CARTO.
- **No JavaScript.** The only `script` elements are the two JSON-LD blocks in `index.html`
  (`SoftwareApplication` and `FAQPage`). The questions and answers in the `FAQPage` block are
  word for word those shown on the page: change one, change the other.
- **Colours are tokens.** Ten custom properties on `:root`, redefined under
  `prefers-color-scheme: dark`; no literal colour outside those two blocks. `index.html` and
  `help-v1.css` carry the same two blocks. `--brand` (`#4A90D9`, the program's accent) is
  decoration only: it is too light for small text on white.
- **Internal links name their file** (`help/index.html`, `../index.html`), so the site also works
  opened from disk.
- **Every factual statement comes from the repository**: `README.md`, `docs/USER_GUIDE.md`,
  `docs/PROMPT_LIBRARY_GUIDE.md`, `docs/LICENSING.md`, `prompts/README.md`,
  `THIRD-PARTY-NOTICES.md`. When one of those changes, check the pages that restate it.
- **A sample uses an invented chart or a public figure's published data, never a private
  person's.** On the site now: the two invented people, Eleanor Hartwell and Daniel Whitfield,
  the User Guide's example charts; one public figure, Harry, Duke of Sussex, with his
  published birth time (the rectification sample, whose reading also names members of his
  family where its dated public events do); and the horary reading, a real question of Steve's
  own, there by his decision. The screenshots show only the two invented charts. No CARTO key
  is ever shown; the site tells users to get their own.
- **The prompts are an aid, not a replacement.** The site presents the prompt library as an
  aid to an astrologer, not a replacement for one (Steve, 2026-11-17): an assistant presents
  a report, organises it and highlights the patterns in it, and judging what those patterns
  mean stays with the reader. The main page says so in the samples introduction, the prompt
  library section, the third step and a common question (with its twin in the `FAQPage`
  block); the samples index, the rectification reading's note, the solar return reading's
  note, `help/faq`, `help/first-reading` and `llms.txt` say it too. New text keeps to it.

## Previewing from disk

Open `AstroCast/index.html` in a browser. Every page, image, font and PDF link works from disk.
The download buttons and the other GitHub links go to the live repository.

To see the dark scheme, switch the system (or the browser) to dark. To see the phone layout,
narrow the window below 768 px.

## The per-release step

Every value that changes with a release is marked in the pages: an element with a `data-rel`
attribute holds one value as its text (`version`, `msi-name`, `msi-size`, `msi-sha`, and the
same for `dmg` and `zip`); the three download buttons carry `data-rel="msi-url"` and so on with
the address in their `href`; the comment `rel:version-jsonld` stands before the JSON-LD block
whose `softwareVersion` line carries the version; and `llms.txt` has a `Current version:` line
and three download lines. No release value appears anywhere without its marker.

With the release's three files (the MSI, the DMG and the prompt library zip) in one local
folder:

```powershell
.\Update-WebsiteRelease.ps1 -Version 1.7.2 -ReleaseFolder D:\Releases\1.7.2 -WhatIf
.\Update-WebsiteRelease.ps1 -Version 1.7.2 -ReleaseFolder D:\Releases\1.7.2
```

The first line lists what would change and writes nothing. The script reads the three files'
names and sizes, computes their SHA-256, and rewrites `index.html`, any marked value in the help
pages, and `llms.txt`. It makes no network call. It refuses, changing nothing, if a file is
missing, if a marker is missing or occurs an unexpected number of times, or if a value of the
old release would be left somewhere unmarked. Then commit, and upload the changed files.

If a new marked value is added to `index.html`, add its expected count to `$indexExpected` at
the top of the script.

## How the images were made

The screenshots come from `docs/images/` in this repository; all show only the two invented
charts.

| Site file (PNG and WebP) | Source | Width |
|---|---|---|
| `shot-chart-win-v1` | `chart-panel-win.png` | 1086 (its own size) |
| `shot-chart-mac-v1` | `chart-panel.png` | 1400 |
| `shot-transits-v1` | `transits-panel.png` | 1200 |
| `shot-synastry-v1` | `synastry-panel.png` | 1200 |
| `shot-relocate-v1` | `relocate-panel.png` | 1200 |
| `shot-election-v1` | `election-panel.png` | 1200 |
| `shot-primary-v1` | `primary-panel.png` | 1200 |
| `shot-map-v1` | `map-window.png` | 1200 |

- **Resizing and the PNGs:** PhotoCraft's command line, Image Size with the Lanczos filter,
  keeping the aspect ratio:

  ```powershell
  & 'C:\Program Files\PhotoCraft\photocraft-cli.exe' run docs\images\transits-panel.png --cmd image.imageSize --params '{"width":1200,"resample":"lanczos"}' --out website\AstroCast\images\shot-transits-v1.png
  ```

  `shot-chart-win-v1.png` is a byte copy of its source, which is already smaller than 1400 px.
- **The WebPs:** the same command with a `.webp` output writes a lossless WebP. Two came out
  over 200,000 bytes (`shot-map-v1`, `shot-primary-v1`) and were rewritten from their PNGs as
  lossy WebP, quality 82, with Python's PIL. `shot-chart-win-v1.webp` is PIL's lossless WebP
  (method 6) of the PNG's own pixels, because PhotoCraft's came out larger than the PNG.
- **`favicon.ico`:** the 16, 32 and 48 px images inside the repository's `AstroCast.ico`, stored
  as PNG in a new ICO (Python's PIL).
- **`images/icon-v1.png`:** the ICO's own 128 px image. **`images/apple-touch-icon-v1.png`:**
  its 256 px image reduced to 180 x 180 on the band colour.
- **`images/og-card-v1.png`:** `tools/og-card.html` rendered at 1200 x 630 by Microsoft Edge
  headless:

  ```powershell
  & 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe' --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --window-size=1200,630 --virtual-time-budget=5000 --no-first-run --disable-extensions --user-data-dir=<a scratch folder> --screenshot=<full path>\website\AstroCast\images\og-card-v1.png file:///<full path>/website/tools/og-card.html
  ```

The fonts are byte copies of the PhotoDB website's; `AstroCast/fonts/PROVENANCE.txt` records
where each face comes from. The two PDFs in `AstroCast/docs/` are copies of the guides built
from `docs/USER_GUIDE.md` and `docs/PROMPT_LIBRARY_GUIDE.md`.

Each screenshot on the main page is a link to its own full-size PNG.

## The samples

"See what you get" on the main page links to sample pages: reports AstroCast wrote, each
beside the reading an AI assistant made from it with a prompt of the prompt library. Four
pairs are for the two invented charts (a natal chart, a solar return, a three-month transit
timeline, a synastry). One pair is for a public figure, Harry, Duke of Sussex, with his
published birth time: his primary directions from birth to age 43, and a reading that tests
that birth time against ten dated events. And there is one horary reading of a real question
that Steve chose to share.

The rule: a sample uses an invented chart or a public figure's published data, never a
private person's. The horary reading is the one exception, by Steve's decision; nothing from
it is quoted anywhere else on the site.

The cards run in the same order on the main page and on the samples index: Three months of
transits; A birth time put to the test; A natal chart; The year ahead; Two charts compared; A
question asked of the sky. `sitemap.txt` lists 18 addresses: the main page, the six help
pages, the samples index and the ten listed sample pages.

- **The sources** are in `samples-src/`: one Markdown file for each sample page, under a short
  name (`natal-report.md`, `natal-reading.md`, `solar-return-report.md`,
  `solar-return-reading.md`, `timeline-report.md`, `timeline-reading.md`,
  `rectification-report.md`, `rectification-reading.md`, `synastry-report.md`,
  `synastry-reading.md`, `horary-reading.md`). They are kept as AstroCast or the assistant
  wrote them. A sample is never corrected. Three are not the whole of what was written, and
  their pages say so:
  - `rectification-report.md` is an **excerpt**: the report's header and its Primary
    Directions section, cut at the byte level (the export's first 12 lines, then everything
    from its line `## Primary Directions` to the end), the natal chart left out. The reason:
    the natal chart's medical correspondences and stark keywords would stand beside a living
    person's name, and the reading uses none of them.
  - `rectification-reading.md` is a **transcription**: the assistant delivered a PDF, and the
    source gives that PDF's words as Markdown, checked word for word against the PDF's text.
    The page footers and the chart's axis labels are left out, and the chart's ten values are
    given as a list.
  - The export as AstroCast wrote it (the full report) and the PDF under the name it was
    delivered with are kept outside the repository. `rectification-reading-v1.pdf` in
    `AstroCast/samples/files/` is a byte copy of that PDF.
  - `solar-return-reading.md` is a **transcription** too: the assistant delivered a PDF, and
    the source gives that PDF's words as Markdown, made from the PDF's text and checked word
    for word against it. The eleven page footers and the heading that continues the glossary
    onto the last page are left out.
  - That PDF is the assistant's **second delivery**. The first said that two tables of the
    report gave different declinations for Neptune, which they do not (it had read Saturn's
    row). Told so, the assistant delivered a version that differs only in leaving that remark
    out, and the page's note says so. Nobody but the assistant changed the reading's words,
    so the rule that a sample is never corrected holds (Steve, 2026-11-19).
  - `solar-return-report.md` and its download, `solar-return-report-v1.md`, are the whole
    report as AstroCast wrote it. The PDF as delivered, its first version and the export
    under its own name are kept outside the repository. `solar-return-reading-v1.pdf` in
    `AstroCast/samples/files/` is a byte copy of the PDF as delivered.
- **The pages** in `AstroCast/samples/` are built from those sources and must not be edited by
  hand. To rebuild them:

  ```powershell
  .\tools\Build-Samples.ps1
  ```

  The script runs pandoc on each source (GitHub-flavoured Markdown to an HTML fragment),
  shifts the sample's headings under the page's own `h1`, wraps every table in a region that
  scrolls sideways on its own, and adds the note at the top of each page, the download links
  and the "On this page" list. It also marks the cells of each table column that holds running
  text (class `wrap`) and, beside such a column, longer phrases (class `phrase`), so that
  under 1024 px `samples-v1.css` keeps every other cell on one line, gives a wrapping column
  a readable width, and lets a table scroll sideways before its cells are squeezed; and it
  puts one line above a sample's first table, shown under 768 px only, that says so (class
  `table-hint`, the one text inside a sample's `article` that is not the sample's own). It
  makes no network call, and from the same sources, files and pandoc version it writes the
  same bytes every time. The pages were made with pandoc 3.8.2.1.
  A reading page leads with its PDF: under the note it holds a box (class `delivered`) with
  a button (class `btn`) to the PDF as the assistant delivered it, the button's text giving
  the PDF's page count and size; then, under the heading "The same reading as text" (id
  `text-version`), the text version: the "On this page" list and the sample. The page counts
  are fields in the script's `$Samples` table (`PdfPages`), to be set when a PDF is replaced;
  the sizes are read from the files. The rectification reading's box has a sentence of its
  own (`Delivered`), which names the PDF's chart.
  The notes, titles and card texts are in the script's `$Samples` and `$Cards` tables. A
  sample takes the standard note of its kind, or a note of its own (`Note`): the
  rectification pair has its own, and its report's download is worded as an excerpt
  (`MarkdownText`); the solar return reading has a note of its own too, which says how the
  reading is shown and that it is the assistant's second delivery.
- **The downloads** are in `AstroCast/samples/files/`: the PDFs, and for each report the
  Markdown file as AstroCast wrote it (for the rectification report, the excerpt:
  `rectification-report-v1.md`; its reading's PDF is `rectification-reading-v1.pdf`). The
  solar return's two are `solar-return-report-v1.md` and `solar-return-reading-v1.pdf`. They
  are byte copies with versioned names (`natal-report-v1.pdf`).
- **A changed sample gets a new versioned file name.** Replace its source in `samples-src/`,
  add the new download as `...-v2`, change the name in the script's `$Samples` table, remove
  the old file, and run the script. The pages themselves are HTML and are always fetched
  fresh.
- **The horary page is unlisted on purpose.** It stays linked from the main page's card and
  from the samples index, but it is not offered to search engines: the build script (the
  `NoIndex` entry in `$Samples`) gives its page a robots `noindex` meta and no canonical
  address, `AstroCast/.htaccess` sends `X-Robots-Tag: noindex` with its PDF, and neither
  `sitemap.txt` nor `llms.txt` names it.

How Git stores them: `website/.gitattributes` declares the PDFs, fonts and images binary and
the Markdown downloads in `AstroCast/samples/files/` `-text`, so all of them are stored and
checked out byte for byte. (Without it Git took the smallest PDF for text, and a Windows
checkout would have rewritten its line endings.) The Markdown sources in `samples-src/` are
ordinary text, stored with LF line endings (the repository's `* text=auto`), so a source that
was written with CRLF or mixed endings has a different hash inside Git than it had when it
was written. The build script reads either.

## The host's cache, and why assets carry `-v1`

The host serves a ten-year `Cache-Control` and `Expires` on every asset type it has a rule for,
and that wins over this folder's `.htaccess` for everything but `.html`. A visitor's browser
may therefore keep a stylesheet, an image, a font or a PDF for years.

So every asset carries a version in its name (`help-v1.css`, `samples-v1.css`,
`shot-map-v1.png`, `AstroCast_User_Guide-v1.pdf`, `natal-report-v1.pdf`). **A changed asset gets a new versioned name** (`-v2`), and the
pages that use it are changed to match; the HTML is always fetched fresh, so the new name
takes effect at once. Never replace the contents of a versioned file under the same name.
`favicon.ico` is requested by that fixed name and cannot be versioned. Two exceptions are on
record. On 2026-11-17 the Prompt Library Guide's PDF was replaced under its `-v1` name, by
Steve's decision, because the site had not yet been uploaded and no copy was cached anywhere.
On 2026-11-18 `samples-v1.css` gained rules under its name for the same reason.
Once the site is uploaded, the rule has no exception.

## The upload

Steve uploads by hand, with WinSCP: the whole of `website\AstroCast` to the server folder
`/public/AstroCast`. Nothing else in `website\` is uploaded. After a release, upload the files
the release step changed (`index.html`, `llms.txt`, and any help page it names).
