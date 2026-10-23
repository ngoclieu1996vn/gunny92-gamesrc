using System;
using System.Reflection;
class MagicPowerTests
{
 static Type helper;
 static object Call(string name,params object[] args){return helper.GetMethod(name).Invoke(null,args);}
 static void E(object actual,object expected,string label){if(!actual.Equals(expected))throw new Exception(label+": "+actual+" != "+expected);}
 static int Main(string[] args){try{
  helper=Assembly.LoadFrom(args[0]).GetType("Game.Server.Packets.MagicPowerV92");
  if(helper==null)throw new Exception("Experimental magic fight-power helper must exist; legacy server has no magic contribution.");
  E(Call("Calculate",1000,1000.0,100L,0L),2009,"100 MA at ST1000");
  E(Call("Calculate",1000,1000.0,0L,100L),2009,"MR has equal power weight");
  E(Call("Calculate",1000,1000.0,100L,100L),3019,"round once after combining MA/MR");
  E(Call("Calculate",1000,816.0,100L,0L),1563,"low measured ST");
  E(Call("Calculate",1000,1253.0,1L,0L),1019,"observed ST1253");
  E(Call("Calculate",1000,1300.0,100L,0L),3178,"upper calibration endpoint");
  foreach(double st in new[]{0.0,799.0,1301.0,1e100,double.NaN,double.PositiveInfinity})E(Call("Calculate",12345,st,100L,100L),12345,"uncalibrated ST must retain legacy power");
  E(Call("Calculate",12345,1000.0,0L,0L),12345,"zero magic preserves baseline including HP");
  E(Call("Calculate",12345,1000.0,-10L,-20L),12345,"invalid negative totals add nothing");
  E(Call("Calculate",int.MaxValue,1000.0,100L,0L),int.MaxValue,"int saturation");
  E(Call("Calculate",1000,1000.0,long.MaxValue,long.MaxValue),int.MaxValue,"sum must not overflow long");
  E(Call("IsEnabledForPlayer",228,false,"228"),false,"default/off gate");
  E(Call("IsEnabledForPlayer",228,true,""),false,"no allowlist means deny");
  E(Call("IsEnabledForPlayer",228,true,"*"),false,"no wildcard global rollout");
  E(Call("IsEnabledForPlayer",228,true," 228, 229 "),true,"explicit test account");
  E(Call("IsEnabledForPlayer",227,true,"228"),false,"real account excluded");
  E(Call("IsEnabledForPlayer",22,true,"228"),false,"exact ID matching");
  Console.WriteLine("PASS: estimated MA/MR weights, calibration bounds, baseline/HP retention, rounding, saturation and explicit test-account gating.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
