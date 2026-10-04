import hashlib, json
from pathlib import Path
from pypdf import PdfReader
pairs = [
    ('cached',Path('artifacts/benchmarks/matrix/inProcess-repeated-10-1/result-0-0.pdf'),Path('artifacts/benchmarks/matrix/inProcess-repeated-10-1/result-2-0.pdf')),
    ('uncached',Path('artifacts/benchmarks/uncached/inProcess-baseline-10-1/result-2-0.pdf'),Path('artifacts/benchmarks/matrix/inProcess-repeated-10-1/result-2-0.pdf')),
    ('worker',Path('artifacts/benchmarks/worker-final/worker-repeated-10-1/result-3-0.pdf'),Path('artifacts/benchmarks/matrix/inProcess-repeated-10-1/result-2-0.pdf')),
    ('linux',Path('artifacts/benchmarks/linux/inProcess-repeated-10-1/result-2-0.pdf'),Path('artifacts/benchmarks/matrix/inProcess-repeated-10-1/result-2-0.pdf'))]
report=[]
for label,left,right in pairs:
    a,b=PdfReader(left),PdfReader(right)
    ta,tb='\n'.join(p.extract_text() for p in a.pages),'\n'.join(p.extract_text() for p in b.pages)
    images_a=[hashlib.sha256(i.data).hexdigest() for p in a.pages for i in p.images]
    images_b=[hashlib.sha256(i.data).hexdigest() for p in b.pages for i in p.images]
    assert len(a.pages)==len(b.pages) and ta==tb and images_a==images_b, label
    assert 'Кириллица' in ta and 'SVG' in ta
    report.append(dict(scenario=label,pages=len(a.pages),textSha256=hashlib.sha256(ta.encode()).hexdigest(),images=images_a))
Path('artifacts/pdf-verification/content-comparison.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('PDF text/pages/image content comparison passed:',len(report))
