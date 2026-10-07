import fs from 'node:fs/promises';
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load('../../밸런스시트.xlsx'));
const image=await wb.render({sheetName:'범례',range:'A17:D32',scale:1});
await fs.writeFile('workbook-before.png',new Uint8Array(await image.arrayBuffer()));
console.log('preview saved');
