/** Build reviewable English ASO assets from untouched production captures. */
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const aso = path.join(root, 'store/aso');
const out = path.join(aso, 'creative/en-US');
const esc = text => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
const cards = fs.readFileSync(path.join(root, 'store/src/mark.svg'), 'utf8');
const plan = [
  ['03', ['Poker against', 'the dealer']],
  ['02', ['Pick your kicker']],
  ['04', ['Choose your', 'pressure round']],
  ['05', ['Track your record']],
  ['06', ['Learn the rules']],
  ['01', ['Play offline.', 'Play-money chips.']],
];
const background = (w,h) => `<rect width="${w}" height="${h}" fill="#14522C"/><circle cx="${w/2}" cy="${h/2}" r="${w}" fill="url(#felt)"/>`;
const defs = '<defs><radialGradient id="felt"><stop stop-color="#2A8A4E"/><stop offset="1" stop-color="#0A3419"/></radialGradient></defs>';
const svg = (w,h,body) => `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${w}" height="${h}">${defs}${background(w,h)}${body}</svg>`;
const caption = (lines, size=62) => lines.map((text,i) => `<text x="540" y="${106+i*74}" text-anchor="middle" font-family="Arial, sans-serif" font-weight="700" font-size="${size}" fill="white">${esc(text)}</text>`).join('');
async function screenshot(source, lines, name) {
  const input=path.join(aso,'sources/en-US',source+'.png');
  const image=await sharp(input).resize({width:740,height:1644,fit:'inside'}).png().toBuffer();
  const overlay = `<rect x="160" y="244" width="760" height="1664" rx="24" fill="#0A2B15"/><image x="170" y="254" width="740" height="1644" xlink:href="data:image/png;base64,${image.toString('base64')}"/>`;
  const sourceSvg=svg(1080,1920,caption(lines)+overlay);
  fs.writeFileSync(path.join(out,name+'.svg'),sourceSvg);
  await sharp(Buffer.from(sourceSvg)).removeAlpha().png().toFile(path.join(out,name+'.png'));
  fs.writeFileSync(path.join(out,name+'.html'),`<!doctype html><meta charset="utf-8"><title>${esc(lines.join(' '))}</title><style>body{margin:0;background:#14522c;color:white;font-family:Arial}main{width:1080px;height:1920px;position:relative;background:radial-gradient(#2a8a4e,#0a3419)}h1{margin:0;padding-top:48px;line-height:74px;text-align:center;font-size:62px}img{position:absolute;left:170px;top:254px;width:740px;height:1644px;object-fit:contain}</style><main><h1>${lines.map(esc).join('<br>')}</h1><img src="../../sources/en-US/${source}.png" alt="Unedited production screenshot"></main>`);
}
async function main() {
  fs.mkdirSync(out,{recursive:true});
  for(let i=0;i<plan.length;i++) await screenshot(...plan[i],`screenshot-${String(i+1).padStart(2,'0')}`);
  const fg = svg(1024,500, `<svg x="34" y="30" width="350" height="350" viewBox="0 0 1024 1024"><g transform="translate(512 512) scale(1.78) translate(-468 -599)">${cards}</g></svg><text x="400" y="170" font-family="Arial,sans-serif" font-size="60" font-weight="700" fill="white">Poker vs Dealer</text><text x="402" y="242" font-family="Arial,sans-serif" font-size="32" fill="#FFC93C">One hand. Two contests.</text><text x="402" y="305" font-family="Arial,sans-serif" font-size="28" fill="white">Offline • Play-money chips</text>`);
  fs.writeFileSync(path.join(out,'featureGraphic.svg'),fg);
  await sharp(Buffer.from(fg)).removeAlpha().png().toFile(path.join(out,'featureGraphic.png'));
  await screenshot('03',['One hand.', 'Two contests.'],'first-screenshot-challenger');
  fs.writeFileSync(path.join(out,'captions.json'),JSON.stringify(plan.map(([source,lines],i)=>({sequence:i+1,source,caption:lines.join(' ')})),null,2)+'\n');
  const copy=JSON.parse(fs.readFileSync(path.join(aso,'metadata/en-US/listing.json')));
  const counts={};
  for(const [field,limit] of [['title',30],['shortDescription',80],['fullDescription',4000]]) {
    counts[field]=copy[field].length;
    if(!counts[field]||counts[field]>limit) throw Error(field+' limit');
    fs.writeFileSync(path.join(aso,'metadata/en-US',field+'.txt'),copy[field]+'\n');
  }
  fs.writeFileSync(path.join(aso,'index.html'),`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Poker vs Dealer — ASO review</title><style>body{max-width:1300px;margin:40px auto;padding:24px;background:#f5f4ed;font:18px Arial;color:#132c20}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:18px}.grid img{width:100%;border-radius:12px}pre{white-space:pre-wrap;line-height:1.5;font:inherit}a{color:#075b36}</style><h1>${esc(copy.title)}</h1><p>${esc(copy.shortDescription)}</p><p>Production v3 screenshots. Version 4 with new dealer artwork has been submitted; production approval is pending.</p><img style="width:100%;max-width:1024px" src="creative/en-US/featureGraphic.png" alt="Feature graphic"><h2>Six screenshots</h2><div class="grid">${plan.map(([_,lines],i)=>`<div><p>${esc(lines.join(' '))}</p><a href="creative/en-US/screenshot-${String(i+1).padStart(2,'0')}.html"><img src="creative/en-US/screenshot-${String(i+1).padStart(2,'0')}.png" alt="${esc(lines.join(' '))}"></a></div>`).join('')}</div><h2>Description</h2><pre>${esc(copy.fullDescription)}</pre><p><a href="calendar.md">90-day calendar</a> · <a href="reports/baseline.md">Baseline</a></p></html>`);
  const entries=[];
  for(const [source] of plan) {
    const xml=fs.readFileSync(path.join(aso,'sources/en-US',source+'.xml'),'utf8');
    if(!xml.includes('package="com.micorlov.pokervsdealer"')) throw Error('Wrong screenshot package');
  }
  for(const file of fs.readdirSync(out).filter(x=>x.endsWith('.png'))) {
    const {width,height,channels}=await sharp(path.join(out,file)).metadata();
    if(file==='featureGraphic.png' ? width!==1024||height!==500 : width!==1080||height!==1920) throw Error('Dimensions '+file);
    entries.push({file,width,height,channels,sha256:require('crypto').createHash('sha256').update(fs.readFileSync(path.join(out,file))).digest('hex')});
  }
  fs.mkdirSync(path.join(aso,'reports'),{recursive:true});
  fs.writeFileSync(path.join(aso,'reports/validation.json'),JSON.stringify({package:'com.micorlov.pokervsdealer',locale:'en-US',counts,limits:{title:30,shortDescription:80,fullDescription:4000},assets:entries,rawUiEvidence:true,visualReview:'pending'},null,2)+'\n');
  const contact=await Promise.all(plan.map(async(_,i)=>({input:await sharp(path.join(out,`screenshot-${String(i+1).padStart(2,'0')}.png`)).resize(270,480).png().toBuffer(),left:i*270,top:0})));
  await sharp({create:{width:1620,height:480,channels:3,background:'#14522c'}}).composite(contact).png().toFile(path.join(aso,'reports/screenshots-contact.png'));
  console.log(JSON.stringify({locale:'en-US',counts,screenshots:6,featureGraphics:1}));
}
main().catch(error=>{console.error(error);process.exit(1)});
