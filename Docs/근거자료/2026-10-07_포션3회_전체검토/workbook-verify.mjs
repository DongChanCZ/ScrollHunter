import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from '@oai/artifact-tool';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load('../../밸런스시트.xlsx'));
for(const [s,r] of [['적','A4:J8'],['인카운터','A1:H15'],['스킬','A37:S41'],['스킬','J4:N8']]){const b=await wb.render({sheetName:s,range:r,scale:1});await fs.writeFile(`saved-${s}-${r.replace(':','-')}.png`,new Uint8Array(await b.arrayBuffer()));}
const s=wb.worksheets.getItem('HP예산');
s.getRange('C5').values=[[999]];s.getRange('C6:C9').values=[[100],[50],[200],[300]];
console.log((await wb.inspect({kind:'table',range:'HP예산!A13:B17',include:'values',tableMaxRows:5,tableMaxCols:2,maxChars:1000})).ndjson);
s.getRange('C6').values=[[null]];
console.log((await wb.inspect({kind:'table',range:'HP예산!E6:E9',include:'values',tableMaxRows:4,tableMaxCols:1,maxChars:1000})).ndjson);
// No export: these boundary inputs exist only in the disposable imported workbook.
