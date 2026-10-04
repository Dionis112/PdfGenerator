import hashlib,json
from pathlib import Path
from pypdf import PdfReader
root=Path('artifacts/benchmarks')
reference=root/'linux-v2/inProcess-repeated-10-1/result-2-0.pdf'
pairs=[('cold-warm',root/'linux-v2/inProcess-repeated-10-1/result-0-0.pdf'),('uncached',root/'linux-v2-control/inProcess-baseline-10-1/result-2-0.pdf'),('windows',root/'final-windows/inProcess-repeated-10-1/result-3-0.pdf'),('worker-restart',root/'oneshot/result-2-0.pdf')]
def content(p):
 r=PdfReader(p); t='\n'.join(x.extract_text() for x in r.pages); i=[hashlib.sha256(x.data).hexdigest() for y in r.pages for x in y.images]
 return len(r.pages),t,i
b=content(reference); report=[]
for label,path in pairs:
 a=content(path); assert a==b,label
 assert 'Кириллица' in a[1] and 'SVG' in a[1]
 report.append(dict(scenario=label,pages=a[0],textSha256=hashlib.sha256(a[1].encode()).hexdigest(),images=a[2],corpusVersion=2))
for label,left,right in [
 ('linux-worker-warm',root/'linux-worker-v2/worker-repeated-10-1/result-2-0.pdf',reference),
 ('large-linux-worker',root/'linux-worker-v2/worker-repeated-100-1/result-2-0.pdf',root/'linux-resource-check/inProcess-repeated-100-1/result-2-0.pdf')]:
 a,b=content(left),content(right); assert a==b,label
 report.append(dict(scenario=label,pages=a[0],textSha256=hashlib.sha256(a[1].encode()).hexdigest(),images=a[2],corpusVersion=2))
Path('artifacts/pdf-verification/content-comparison-v2.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('PDF v2 comparison passed:',len(report))
