// Explicit installed-Brave setup check. Creates/revokes only its own enrollment;
// no focus session, browser policy, or website permissions are applied.
import { chromium, expect } from '@playwright/test';
import { mkdtemp } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import net from 'node:net';
import path from 'node:path';
if (!process.env.LOCKIN_PROBE_PIPE) throw new Error('Supply LOCKIN_PROBE_PIPE for the running local host.');
async function desktop(type,payload={}) {
 const requestId=crypto.randomUUID(); const data=Buffer.from(JSON.stringify({protocolVersion:1,requestId,type,payload})); const header=Buffer.alloc(4);header.writeUInt32LE(data.length);
 return await new Promise((resolve,reject)=>{const socket=net.connect('\\\\.\\pipe\\'+process.env.LOCKIN_PROBE_PIPE); let received=Buffer.alloc(0); const timer=setTimeout(()=>{socket.destroy();reject(new Error('Host timeout'))},10000);socket.on('connect',()=>socket.write(Buffer.concat([header,data])));socket.on('error',e=>{clearTimeout(timer);reject(e)});socket.on('data',chunk=>{received=Buffer.concat([received,chunk]);if(received.length>=4&&received.length>=4+received.readUInt32LE(0)){clearTimeout(timer);socket.end();const r=JSON.parse(received.subarray(4).toString());if(r.requestId!==requestId||!r.ok)reject(new Error(r.error??'Invalid response'));else resolve(r.result)}})});
}
const extension=path.resolve('dist'); const folder=await mkdtemp(path.join(tmpdir(),'lockin-brave-probe-'));
const label='Setup verification '+crypto.randomUUID();
const context=await chromium.launchPersistentContext(folder,{executablePath:'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe',headless:true,args:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]});
try {
 const worker=context.serviceWorkers()[0]??await context.waitForEvent('serviceworker',{timeout:15000});
 const page=await context.newPage();await page.setViewportSize({width:380,height:760});await page.goto(`chrome-extension://${new URL(worker.url()).host}/popup.html`);
 await expect(page.locator('#status')).toContainText('Desktop connected',{timeout:15000});
 const {code}=await desktop('BeginBrowserPairing');
 await page.locator('details').filter({has:page.locator('#label')}).locator('summary').click();
 await page.locator('#label').fill(label);await page.locator('#code').fill(code);
 await expect(page.locator('#pairing')).toBeHidden({timeout:15000});
 await expect.poll(async()=> (await desktop('GetBrowserConnections')).some(b=>b.label===label&&b.connected)).toBe(true);
 await expect(page.locator('#error')).toBeEmpty();
 await page.screenshot({path:'../docs/images/brave-connected-2026-10-01.png',fullPage:true});
 console.log('PASS: installed Brave lookup, desktop probe, paste-to-pair popup, and host connection. No session started.');
} finally {
 try {
  const browsers=await desktop('GetBrowserConnections');
  for(const b of browsers.filter(b=>b.label===label)) await desktop('RevokeBrowser',{installationId:b.installationId});
 } finally { await context.close(); }
}
