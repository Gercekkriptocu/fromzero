import json,pathlib,re
root=pathlib.Path(__file__).resolve().parents[1];data=root/'tools/LocaleCatalog/Data';items=json.loads((data/'locale-strings.json').read_text(encoding='utf-8'));out={}
for line in (data/'locale-en-lines.txt').read_text(encoding='utf-8-sig').splitlines():
 if '|' not in line:continue
 n,en=line.split('|',1);raw=items[int(n)]['Text'];en=en.replace('\\n','\n');prefix=re.match(r'^\s*',raw)[0];suffix=re.search(r'\s*$',raw)[0];out[raw]=prefix+en.strip()+suffix
 if raw.strip():out.setdefault(raw.strip(),en.strip())
for row in (row for asset in ('ThirdParty/Reclaim/targets.json','Assets/cleanup-additions.json') for row in json.loads((root/'src/FromZero'/asset).read_text(encoding='utf-8-sig'))):
 for a,b in [('nameTr','name'),('descriptionTr','description')]:
  if row.get(a) and row.get(b):out[row[a]]=row[b]
extra=root/'src/FromZero/Assets/locale-extra.json'
if extra.exists():out.update(json.loads(extra.read_text(encoding='utf-8')))
for patch in sorted((root/'src/FromZero/Assets').glob('locale-additions-*.json')):
 out.update(json.loads(patch.read_text(encoding='utf-8')))
(root/'src/FromZero/Assets/locale-en.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
print('Locale entries:',len(out))
