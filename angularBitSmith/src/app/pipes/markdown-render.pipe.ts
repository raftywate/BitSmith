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

  // Mark code blocks with tabs
  processed = processed.replace(/```\s*([a-zA-Z0-9\-\+#]+)\s*\[\]/g, '```$1-TAB-GROUP');

  let html = marked.parse(processed) as string;

  // Process HTML code tabs
  html = groupHtmlCodeTabs(html);

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

function groupHtmlCodeTabs(html: string): string {
  // Find sequences of <pre><code class="language-X-TAB-GROUP">...</code></pre>
  const blockRegex = /<pre><code class="language-([a-zA-Z0-9\-\+#]+)-TAB-GROUP">([\s\S]*?)<\/code><\/pre>/g;
  
  const matches = [...html.matchAll(blockRegex)];
  if (matches.length === 0) return html;
  
  let result = '';
  let lastIndex = 0;
  let currentGroup: RegExpMatchArray[] = [];
  
  for (let i = 0; i < matches.length; i++) {
    const match = matches[i];
    const prevMatch = matches[i - 1];
    
    if (prevMatch) {
      const between = html.substring(prevMatch.index! + prevMatch[0].length, match.index!);
      if (between.trim() === '') {
        currentGroup.push(match);
      } else {
        result += renderHtmlGroup(currentGroup);
        result += between;
        currentGroup = [match];
      }
    } else {
      result += html.substring(lastIndex, match.index!);
      currentGroup = [match];
    }
    lastIndex = match.index! + match[0].length;
  }
  
  if (currentGroup.length > 0) {
    result += renderHtmlGroup(currentGroup);
  }
  
  result += html.substring(lastIndex);
  return result;
}

function renderHtmlGroup(group: RegExpMatchArray[]): string {
  const groupId = Math.random().toString(36).substring(2, 9);
  let html = `<div class="code-tabs">`;
  
  group.forEach((match, index) => {
    const tabId = `tab-${groupId}-${index}`;
    html += `<input type="radio" name="group-${groupId}" id="${tabId}" ${index === 0 ? 'checked' : ''}>`;
  });
  
  html += `<div class="code-tabs__labels">`;
  group.forEach((match, index) => {
    const lang = match[1];
    const tabId = `tab-${groupId}-${index}`;
    html += `<label for="${tabId}">${lang}</label>`;
  });
  html += `</div>`;
  
  html += `<div class="code-tabs__panes">`;
  group.forEach((match, index) => {
    const lang = match[1];
    const codeHtml = match[2];
    html += `<div class="code-tab-pane pane-${index}"><pre><code class="language-${lang}">${codeHtml}</code></pre></div>`;
  });
  html += `</div>`;
  
  html += `</div>`;
  return html;
}

