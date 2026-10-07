import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import {FileBlob,SpreadsheetFile} from '@oai/artifact-tool';
const path='../../밸런스시트.xlsx';
const original=await fs.readFile(path),hash=b=>crypto.createHash('sha256').update(b).digest('hex');
await fs.writeFile('workbook-before.xlsx.bak',original);
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path));
const edits=[];
function set(sheet,cell,value){const r=wb.worksheets.getItem(sheet).getRange(cell);edits.push({sheet,cell,before:r.formulas?.[0]?.[0]||r.values?.[0]?.[0],after:value}); if(typeof value==='string'&&value.startsWith('='))r.formulas=[[value]];else r.values=[[value]];}
for(const [s,c,v] of [
['범례','C29',3],['범례','D29','10 A46. 시작 3/3, PS01 최대치·현재 +1'],
['범례','B30','튜토리얼 제외 A+B / C / A+B+C'],['범례','C30',2],['범례','D30','(2+1+3)/3=2. 10 A32·A34'],
['범례','A32','2026-10-07: 포션 3/3·전투 구성·적 피해 갱신. 10 A32·A33·A38·A43·A46.'],
['스킬','L4','코스트당 (1체)'],['스킬','A39','L열은 1체, M열은 일반 전투 평균 적 수 기준. 실제 대상 수·시전 시간을 함께 비교.'],
['스킬','A40','보스전은 본체 1체+오브 최대 2체. 광역 가치는 활성 오브 수에 따라 달라짐(10 A43).'],
['적','A5','약탈자 A'],['적','A6','궁병 B'],['적','A7','우두머리 C'],['적','D5',20],['적','D7',240],
['적','J7','빨강: 방어력 20 기준 200. 방어도 보유 시 100 후 흡수'],
['적','A13','현재 HP·피해·명칭: 10 A30·A33·A38. 튜토리얼·보스·오브는 04 참조.'],
['HP예산','A2','튜토리얼 뒤 HP·포션 초기화. 런 예산은 일반 4전투만 합산. 피해 입력 전 결과 미확인.'],
['HP예산','A5',0],['HP예산','B5','튜토리얼'],['HP예산','A6',1],['HP예산','B6','A+B'],['HP예산','A7',2],['HP예산','B7','C'],['HP예산','A8',3],['HP예산','B8','A+B+C'],['HP예산','A9',4],['HP예산','B9','마법사+오브'],
['HP예산','H5','終了後 초기화. 아래 런 예산에서 제외'.replace('終了後','종료 후')],
['HP예산','B13','=IF(COUNT(C6:C9)<4,"",SUM(C6:C9))'],['HP예산','B14','=IF(COUNT(D6:D9)<4,"",SUM(D6:D9))'],
['HP예산','D15','기본 포션 3개 × 최대 HP 30%. PS01 제외'],
['인카운터','A2','현재 구성: 튜토리얼→A+B→C→A+B+C→보스. 5행은 예시, 실측은 07에 조건과 함께 기록.'],
['인카운터','E14','목표 미입력. 초기 시간 목표는 재검토 전'],
['인카운터','C14','=IF(COUNT(C6:C10)<5,"",SUM(C6:C10))'],['인카운터','C15','=IF(COUNT(D6:D10)<5,"",SUM(D6:D10))']]) set(s,c,v);
for(let r=5;r<=9;r++){
 for(const [out,input] of [['E','C'],['F','D']])set('HP예산',`${out}${r}`,r<=6?`=IF(NOT(ISNUMBER(${input}${r})),"",'직업'!$B$5-${input}${r})`:`=IF(OR(NOT(ISNUMBER(${input}${r})),NOT(ISNUMBER(${out}${r-1}))),"",${out}${r-1}-${input}${r})`);
 set('HP예산',`G${r}`,`=IF(NOT(ISNUMBER(F${r})),"",IF(F${r}<=0,"사망",IF(F${r}<100,"위험","안전")))`);
}
for(const [i,name] of ['튜토리얼','A+B','C','A+B+C','마법사+오브'].entries()){set('인카운터',`A${i+6}`,i);set('인카운터',`B${i+6}`,name);}
wb.recalculate();
const errors=await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#NULL!',options:{useRegex:true,maxResults:30},maxChars:2000});
await fs.writeFile('workbook-errors.json',errors.ndjson);
console.log(errors.ndjson);
for(const [sheet,range] of [['범례','A17:D32'],['적','A4:J15'],['HP예산','A1:H19'],['인카운터','A1:H15'],['스킬','J4:N18'],['스킬','A22:C41']]){
 const b=await wb.render({sheetName:sheet,range,scale:1});await fs.writeFile(`workbook-${sheet}-${range.replace(':','-')}.png`,new Uint8Array(await b.arrayBuffer()));
}
console.log((await wb.inspect({kind:'table',range:'HP예산!A13:D19',include:'values,formulas',tableMaxRows:7,tableMaxCols:4,maxChars:2500})).ndjson);
await fs.writeFile('workbook-changes.json',JSON.stringify(edits,null,2));
if(hash(await fs.readFile(path))!==hash(original))throw Error('Workbook changed externally');
await (await SpreadsheetFile.exportXlsx(wb)).save(path);
console.log('saved',edits.length,'cell edits');
