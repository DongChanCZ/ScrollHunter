const {chromium}=require('C:/Users/MBC-501-19/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'../../..'),base=path.join(root,'Assets/WebGLTemplates/ScrollHunter');
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true});
 try{
 const page=await browser.newPage();
 await page.route('http://scrollhunter.test/**',async route=>{
  const name=new URL(route.request().url()).pathname;
  if(name.endsWith('.ttf'))return route.fulfill({contentType:'font/ttf',body:fs.readFileSync(path.join(base,'Cinzel-Bold.ttf'))});
  let html=fs.readFileSync(path.join(base,'index.html'),'utf8').replace(/<script src="Build\/.*?<\/script>/,'').replace(/\{\{\{ JSON.stringify\(.*?\) \}\}\}/g,'"test"');
  return route.fulfill({contentType:'text/html',body:html});
 });
 await page.goto('http://scrollhunter.test/');
 await page.evaluate(()=>document.fonts.ready);
 const rows=[];
 for(const [width,height] of [[360,240],[640,388],[960,568],[1920,1080]]){
  await page.setViewportSize({width,height});
  for(let lang=0;lang<5;lang++){
   await page.selectOption('#language',String(lang));
   const r=await page.evaluate(()=>{
    const rect=s=>{let r=document.querySelector(s).getBoundingClientRect();return {x:r.x,y:r.y,w:r.width,h:r.height,right:r.right,bottom:r.bottom};};
    return {select:rect('#language'),play:rect('#play'),row:rect('.launch-controls'),title:rect('h1'),font:document.fonts.check('700 32px Cinzel'),family:getComputedStyle(document.querySelector('h1')).fontFamily};
   });
   assert(r.font && r.family.startsWith('Cinzel'));
   assert(Math.abs(r.select.y+r.select.h/2-r.play.y-r.play.h/2)<1);
   assert(r.select.right<r.play.x && r.row.right<=width && r.row.x>=0);
   assert(r.row.y-r.title.bottom>=20);
   rows.push({width,height,lang,...r});
  }
  if(width===960)await page.screenshot({path:path.join(__dirname,'web-start-960.png')});
 }
 // 기존 언어 전달·진행률·성공/실패 흐름도 가짜 Unity 로더로 확인한다.
 await page.evaluate(()=>{window.createUnityInstance=async(c,config,progress)=>{window.testConfig=config;progress(.5);return {};};});
 await page.click('#play');
 assert(await page.locator('#loading').isHidden());
 assert.equal(await page.evaluate(()=>window.scrollHunterLaunchLanguage),4);
 const messages=[];page.on('console',m=>messages.push(m.type()));
 await page.evaluate(()=>{testConfig.showBanner('test warning','warning');testConfig.showBanner('test error','error');});
 assert.deepEqual(messages,['warning','error']);
 await page.reload();
 assert.equal(await page.inputValue('#language'),'4');
 await page.evaluate(()=>{window.createUnityInstance=async()=>{throw Error('test load error');};});
 await page.click('#play');
 await page.waitForFunction(()=>document.getElementById('status').textContent.includes('No se pudo'));
 fs.writeFileSync(path.join(__dirname,'browser-checks.json'),JSON.stringify({layoutCombinations:rows.length,rows,launchLanguage:4,warningErrorPreserved:true,loadErrorShown:true},null,2));
 console.log('PASS: 20 language/viewport layouts, title font, language persistence/launch, loading/error and warning/error routing.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
