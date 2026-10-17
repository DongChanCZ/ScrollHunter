const {chromium}=require('C:/Users/MBC-501-19/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert');
const root='C:/Users/MBC-501-19/Desktop/ScrollHunter_Web_1.3';
(async()=>{
 const server=http.createServer((req,res)=>{
  const name=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
  if(name==='/favicon.ico'){res.writeHead(204);return res.end();}
  const p=path.resolve(root,'.'+(name==='/'?'/index.html':name));
  if(!p.startsWith(path.resolve(root)+path.sep)||!fs.existsSync(p)){res.writeHead(404);return res.end();}
  const bare=p.replace(/\.gz$/,'');const ext=path.extname(bare);const type={'.js':'application/javascript','.wasm':'application/wasm','.html':'text/html','.ttf':'font/ttf','.data':'application/octet-stream'}[ext]||'application/octet-stream';
  res.setHeader('Content-Type',type);if(p.endsWith('.gz'))res.setHeader('Content-Encoding','gzip');
  fs.createReadStream(p).pipe(res);
 });
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const browser=await chromium.launch({channel:'chrome',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1366,height:800}});const logs=[],errors=[];
  page.on('console',m=>logs.push({type:m.type(),text:m.text()}));page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:'+server.address().port+'/');await page.evaluate(()=>document.fonts.ready);
  assert.equal(await page.inputValue('#language'),'1');
  await page.screenshot({path:path.join(__dirname,'web13-launch.png')});
  await page.click('#play');await page.waitForFunction(()=>document.getElementById('loading').style.display==='none',{},{timeout:180000});
  await page.waitForTimeout(3500);
  await page.screenshot({path:path.join(__dirname,'web13-title.png')});
  assert(await page.evaluate(()=>!!window.scrollHunter));
  const report={loaded:true,defaultLanguage:1,errors,logs};
  fs.writeFileSync(path.join(__dirname,'browser-result.json'),JSON.stringify(report,null,2));
  assert.equal(errors.length,0);assert(!logs.some(m=>m.type==='error'));
  console.log('PASS: actual WebGL 1.3 initialized, English default, title screenshot saved, no browser errors.');
 }finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(e=>{console.error(e);process.exitCode=1;});
