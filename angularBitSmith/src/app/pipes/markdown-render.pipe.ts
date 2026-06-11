import { Pipe, PipeTransform, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';
import katex from 'katex';

/**
 * Renders a markdown string (with optional KaTeX math) to safe HTML.
 *
 * Inline math:   $...$      e.g. $O(n \log n)$
 * Display math:  $$...$$    e.g. $$\sum_{i=1}^{n} i = \frac{n(n+1)}{2}$$
 *
 * Standard HTML tags (<sup>, <sub>, &le; etc.) also work directly.
 */
@Pipe({
  name: 'mdRender',
  standalone: true
})
export class MarkdownRenderPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);

  transform(value: string | null | undefined): SafeHtml {
    if (!value) return '';

    try {
      const html = renderMarkdownWithKatex(value);
      return this.sanitizer.bypassSecurityTrustHtml(html);
    } catch (e) {
      // If rendering fails fall back to plain text
      console.error('[MarkdownRenderPipe] render error:', e);
      return this.sanitizer.bypassSecurityTrustHtml(escapeHtml(value));
    }
  }
}

// ─── Helpers ────────────────────────────────────────────────────────────────

/**
 * 1. Extract $$…$$ and $…$ math spans (so marked doesn't mangle them)
 * 2. Run marked for markdown → HTML
 * 3. Re-inject KaTeX-rendered math
 */
function renderMarkdownWithKatex(src: string): string {
  const placeholders: string[] = [];

  // Replace display math $$...$$ first (greedy match over newlines)
  let processed = src.replace(/\$\$([\s\S]+?)\$\$/g, (_, tex) => {
    const idx = placeholders.length;
    try {
      placeholders.push(
        katex.renderToString(tex.trim(), { displayMode: true, throwOnError: false })
      );
    } catch {
      placeholders.push(`<span class="katex-error">$$${tex}$$</span>`);
    }
    return `\x00MATH${idx}\x00`;
  });

  // Replace inline math $...$
  processed = processed.replace(/\$([^\n$]+?)\$/g, (_, tex) => {
    const idx = placeholders.length;
    try {
      placeholders.push(
        katex.renderToString(tex.trim(), { displayMode: false, throwOnError: false })
      );
    } catch {
      placeholders.push(`<span class="katex-error">$${tex}$</span>`);
    }
    return `\x00MATH${idx}\x00`;
  });

  // Parse remaining markdown
  // Preserve raw HTML already in the source (sup, sub, etc.)
  marked.setOptions({ breaks: true });

  let html = marked.parse(processed) as string;

  // Re-inject KaTeX rendered strings
  html = html.replace(/\x00MATH(\d+)\x00/g, (_, i) => placeholders[Number(i)] ?? '');

  return html;
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
