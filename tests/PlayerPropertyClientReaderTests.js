'use strict';
const fs = require('fs');
const assert = require('assert/strict');
const vm = require('vm');

// Execute the shipped client's reader, with only AS type annotations and
// for-each syntax translated. This catches schema drift independently of C#.
const source = fs.readFileSync(process.argv[2], 'utf8');
let body = source.split('protected function __updatePlayerProperty(event:PkgEvent) : void')[1];
assert.ok(body, 'actual client property reader must be present');
body = body.slice(body.indexOf('{') + 1, body.indexOf('public function get propertyAdditions'));
body = body.slice(0, body.lastIndexOf('}'));
body = body.replace(/:((?:int|Object|SoulMarkInfo|String|PackageIn|Array|DictionaryData|PlayerInfo|Boolean))\b/g, '')
    .replace(/for each\(var (_loc\d+_) in (_loc\d+_)\)/g, 'for (var $1 of $2)');

function read(file) {
    const bytes = fs.readFileSync(file);
    let offset = 0;
    const player = {isSelf:true, cultureMachiningDic:{}, cultureQuenchingDic:{}, cultureQuenchingSkillDic:{}, soulMarkDic:{}, commitChanges(){}};
    const syah = {};
    const context = {
        event:{pkg:{
            readInt(){assert.ok(offset + 4 <= bytes.length, `packet164 EOF at ${offset}/${bytes.length}`); const n = bytes.readInt32BE(offset);offset += 4;return n;},
            readUTF(){assert.ok(offset + 2 <= bytes.length, 'UTF length EOF');const n=bytes.readUInt16BE(offset);offset+=2;assert.ok(offset+n<=bytes.length, 'UTF data EOF');const s=bytes.toString('utf8',offset,offset+n);offset+=n;return s;}
        }},
        DictionaryData:function(){}, SoulMarkInfo:function(){}, int:Number,
        findPlayer(id,zone){assert.equal(id,228);assert.equal(zone,1001);return player;},
        SyahManager:{Instance:syah}, FineSuitManager:{Instance:{updateFineSuitProperty(){}}},
        TotemManager:{instance:{updatePropertyAddtion(){}}},
        HorseCoreManager:{instance:{dealHorseCoreAddPro(p){p.propertyAddition.realDamage={horseCore:0};p.propertyAddition.realDefence={horseCore:0};}}},
        dispatchEvent(){}, Event:function(){}
    };
    vm.runInNewContext(body, context, {timeout:1000});
    assert.equal(offset, bytes.length, 'client must consume the entire packet exactly');
    return {p:player, a:player.propertyAddition, syah};
}

const {p,a,syah} = read(process.argv[3]);
assert.equal(a.Attack.Texp,101);assert.equal(a.Defence.Texp,102);assert.equal(a.Agility.Texp,103);assert.equal(a.Luck.Texp,104);
assert.equal(a.HP.Texp,7654);assert.equal(a.HP.Avatar,8765);assert.equal(a.HP.gem,3456);
assert.equal(a.HP.mark,4567);assert.equal(a.HP.petseal,5678);
assert.equal(a.MagicAttack.MagicStone,1234);assert.equal(a.MagicAttack.Horse,2345);
assert.equal(a.MagicDefence.MagicStone,4321);assert.equal(a.MagicDefence.Horse,5432);assert.equal(a.MagicDefence.Temple,6543);
assert.equal(a.MagicAttack.Texp,321);assert.equal(a.MagicAttack.Card,432);assert.equal(a.MagicAttack.mark,543);assert.equal(a.MagicAttack.petseal,654);
assert.equal(a.MagicDefence.Texp,765);assert.equal(a.MagicDefence.Card,876);assert.equal(a.MagicDefence.mark,987);
assert.equal(a.Damage.Avatar,142);assert.equal(a.Armor.Avatar,242);assert.equal(p.markDamage,143);assert.equal(p.markArmor,243);
assert.equal(p.cardAchievementHp,6789);assert.equal(p.cardAchievementDamage,144);assert.equal(p.cardAchievementArmor,244);
assert.equal(syah.totalDamage,901);assert.equal(syah.totalArmor,902);assert.equal(p.fineSuitExp,8642);
assert.equal(p.lingStoneStr,'');assert.equal(p.horseCoreProStr,'');assert.equal(p.realAttack,0);assert.equal(p.highSpeed,0);
const empty = read(process.argv[4]);
assert.equal(empty.a.HP.Texp,0);assert.equal(empty.a.MagicAttack.MagicStone,0);assert.equal(empty.a.MagicDefence.MagicStone,0);
assert.equal(empty.syah.totalDamage,0);assert.equal(empty.p.fineSuitExp,0);
console.log('PASS: packet164 actual client reader consumes exact end; distinct HP, MA/MR, all existing contributions, totals, fine suit and empty defaults survive.');
