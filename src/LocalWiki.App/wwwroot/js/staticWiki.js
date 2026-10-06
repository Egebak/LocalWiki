(() => {
  const input = document.getElementById('wiki-search');
  const results = document.getElementById('search-results');
  const index = window.localWikiSearchIndex || [];
  if (!input || !results) return;
  input.addEventListener('input', () => {
    const q = input.value.trim().toLowerCase();
    results.replaceChildren();
    if (!q) return;
    index.filter(item => (item.title + ' ' + item.aliases.join(' ') + ' ' + item.text).toLowerCase().includes(q)).slice(0, 30).forEach(item => {
      const a = document.createElement('a'); a.href = item.slug + '.html';
      const title = document.createElement('strong'); title.textContent = item.title;
      const path = document.createElement('small'); path.textContent = item.breadcrumb;
      a.append(title, path); results.append(a);
    });
  });
  document.addEventListener('keydown', event => { if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); input.focus(); } if (event.key === 'Escape') { input.value = ''; results.replaceChildren(); } });
})();
