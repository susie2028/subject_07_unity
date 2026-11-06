const fs=require('fs');
const src=fs.readFileSync(process.argv[2],'utf8');
const a=src.indexOf('const FLOOR_DEFS = [');
const b=src.indexOf('const LIFT_FLOORS');
const code=src.slice(a,b).replace('const FLOOR_DEFS =','FLOOR_DEFS =');
let FLOOR_DEFS; eval(code);
fs.writeFileSync('floors.json',JSON.stringify(FLOOR_DEFS,null,1));
console.log(FLOOR_DEFS.map(f=>f.id+' '+f.w+'x'+f.h+' npcs='+f.npcs.length).join('\n'));
const keys=new Set(); FLOOR_DEFS.forEach(f=>Object.keys(f).forEach(k=>keys.add(k))); console.log([...keys].join(' '));
