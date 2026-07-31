namespace RaidRescue
{
    internal static class UiHtml
    {
        public const string Content = @"<!doctype html>
<html>
<head>
<meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
<meta charset=""utf-8"" />
<title>ScrapLab</title>
<style>
*{box-sizing:border-box}
html,body{width:100%;height:100%;margin:0;overflow:hidden;font-family:""Inter Medium"",""Segoe UI"",Arial,sans-serif;color:#f5f5f2}
body{
 background-color:#151719;
 background-image:
  linear-gradient(rgba(51,136,166,.055) 1px,transparent 1px),
  linear-gradient(90deg,rgba(51,136,166,.055) 1px,transparent 1px),
  radial-gradient(circle at 50% -120px,rgba(32,93,112,.34),transparent 510px);
 background-size:28px 28px,28px 28px,auto;
}
button{font:inherit}
.sm-font{font-family:Shentox,""Arial Black"",""Arial Narrow"",sans-serif}
.window-bar{position:fixed;z-index:100;left:0;right:0;top:0;height:38px;display:flex;align-items:center;
 background:linear-gradient(#303435,#1a1d1e);border:1px solid #070808;border-bottom:2px solid #080909;
 box-shadow:inset 0 1px #55595a,0 2px 8px rgba(0,0,0,.7);cursor:default}
.window-grip{height:100%;flex:1;display:flex;align-items:center;min-width:0;padding-left:12px}
.window-emblem{width:30px;height:30px;flex:0 0 30px;margin-right:8px;position:relative;overflow:visible}
.window-emblem.secret-trigger,.window-emblem.secret-trigger *{cursor:default!important;-ms-user-select:none;user-select:none}
.window-emblem.secret-trigger.armed .window-emblem-mark{animation:secretEmblemUnlock .48s cubic-bezier(.16,.84,.3,1) both}
.window-emblem-mark{position:absolute;left:4px;top:4px;width:22px;height:22px;transform:rotate(45deg);
 background:linear-gradient(145deg,#ffd74f,#efa916);border:2px solid #191b1c;border-radius:4px;
 box-shadow:0 0 0 1px #ffd13b,0 2px 0 #070808}
.logo-letter{position:absolute;left:0;top:0;width:100%;height:100%;display:block;transform:rotate(-45deg);overflow:visible}
.logo-letter .logo-world-core{fill:#102f34;stroke:#171b1c;stroke-width:1.2}
.logo-letter .logo-world-line{fill:none;stroke:#58e4f5;stroke-width:1.25;stroke-linecap:round}
.logo-letter .logo-world-glint{fill:none;stroke:#d4fbff;stroke-width:.6;stroke-linecap:round;opacity:.82}
.window-title{min-width:0;color:#f3f3ee;font:11px Shentox,""Arial Black"",sans-serif;letter-spacing:1px;
 white-space:nowrap;overflow:hidden;text-overflow:ellipsis;text-shadow:0 2px #000}
.window-title span{margin-left:9px;color:#8e9495;font:9px ""Inter Medium"",""Segoe UI"",sans-serif;letter-spacing:.5px}
.window-controls{height:100%;display:flex;flex:0 0 auto}
.window-button{position:relative;width:46px;height:36px;padding:0;border:0;border-left:1px solid #101213;
 color:#c7cbcb;background:transparent;cursor:pointer;outline:none}
.window-button:hover{color:#fff;background:#414647}.window-button:active{background:#191b1c}
.window-button.close:hover{background:#c83a22}.window-button.close:active{background:#8f2518}
.window-help-icon{position:absolute;left:50%;top:50%;width:20px;height:20px;display:block;overflow:visible;
 transform:translate(-50%,-50%);color:#ffd046;pointer-events:none}
.window-help-shadow{fill:none;stroke:#111;stroke-width:4}
.window-help-ring{fill:#202324;stroke:#8f6919;stroke-width:2}
.window-help-stem{fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}
.window-help-dot{fill:currentColor}
.window-button.help:hover .window-help-ring{fill:#ffd046;stroke:#fff0a1}
.window-button.help:hover .window-help-icon{color:#272719}
.window-button.minimize:before{content:'';position:absolute;left:17px;top:22px;width:12px;height:2px;background:currentColor}
.window-button.close:before,.window-button.close:after{content:'';position:absolute;left:16px;top:17px;width:14px;height:2px;background:currentColor}
.window-button.close:before{transform:rotate(45deg)}.window-button.close:after{transform:rotate(-45deg)}
.app-scroll{position:fixed;left:0;right:0;top:38px;bottom:0;overflow-y:scroll;overflow-x:hidden;-ms-overflow-style:none}
.scroll-track{display:none;position:fixed;z-index:95;right:4px;top:45px;bottom:7px;width:13px;
 background:#111314;border:1px solid #060707;border-radius:7px;box-shadow:inset 0 0 0 2px #242728,0 1px #4b4f50}
.scroll-track.show{display:block}
.scroll-thumb{position:absolute;left:2px;top:2px;width:7px;min-height:38px;border:1px solid #9b6e12;border-radius:5px;
 background:linear-gradient(90deg,#c88714,#ffd247 48%,#b7770d);box-shadow:inset 1px 0 #fff09a,0 0 5px rgba(255,194,34,.25);
 cursor:pointer}
.scroll-thumb:hover,.scroll-thumb.dragging{border-color:#ffe98d;background:linear-gradient(90deg,#e7a51d,#ffe269 48%,#cf8b12);
 box-shadow:inset 1px 0 #fff8c4,0 0 8px rgba(255,194,34,.48)}
.hazard{position:relative;height:7px;overflow:hidden;background:#292b2c;border-bottom:1px solid #070808;
 box-shadow:0 2px 8px rgba(0,0,0,.65)}
.hazard:before{content:'';position:absolute;left:-102px;top:0;width:calc(100% + 204px);height:7px;
 background:repeating-linear-gradient(135deg,#f7be22 0,#f7be22 18px,#292b2c 18px,#292b2c 36px);
 background-size:51px 51px;transform:translate3d(-51px,0,0);backface-visibility:hidden;
 animation:mainHazardFlow 3.8s linear infinite}
.hazard.paused:before,.scroll-active .hazard:before,.scroll-active .panel-title strong:before,
.scroll-active .state,.scroll-active .note:before,.scroll-active .tutorial-rail:before,
.scroll-active .tutorial-focus:before{animation-play-state:paused}
.shell{max-width:1040px;margin:0 auto;padding:14px 22px 36px}
.topbar{height:56px;display:flex;align-items:center;justify-content:space-between}
.identity{display:flex;align-items:center}
.brand-mark{width:39px;height:39px;margin:0 13px 0 5px;position:relative;transform:rotate(45deg);
 background:linear-gradient(145deg,#ffd74f,#efa916);border:3px solid #191b1c;border-radius:7px;
 box-shadow:0 0 0 2px #ffd13b,0 4px 0 #070808}
.identity h1{margin:0;color:#fff;font:20px/21px Shentox,""Arial Black"",sans-serif;letter-spacing:1.2px;
 text-shadow:0 2px 0 #000}
.identity p{margin:2px 0 0;color:#ffd046;font:10px/13px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1.7px}
.local{color:#afb3b3;font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1px;
 border:1px solid #535758;background:#232628;padding:7px 11px;border-radius:10px;box-shadow:inset 0 1px #35393a}
.local b{display:inline-block;width:7px;height:7px;background:#ffd046;border-radius:50%;margin-right:7px;
 box-shadow:0 0 7px #ffd046}

.panel{position:relative;background:#292c2e;border:1px solid #080909;border-radius:16px 16px 3px 16px;
 box-shadow:inset 0 1px #424647,0 5px 15px rgba(0,0,0,.38);animation:panelAssemble .38s cubic-bezier(.18,.86,.33,1) both}
.panel:after{content:'';position:absolute;right:5px;bottom:5px;width:7px;height:7px;border-right:2px solid #636767;border-bottom:2px solid #636767}
.panel-title{height:32px;display:flex;align-items:center;justify-content:space-between;padding:0 13px 0 15px;
 background:#202224;border-bottom:1px solid #090a0a;border-radius:15px 15px 0 0;box-shadow:inset 0 1px #3b3e40}
.panel-title strong{font:12px Shentox,""Arial Black"",sans-serif;letter-spacing:1.25px;color:#fff}
.panel-title strong:before{content:'';display:inline-block;width:5px;height:16px;background:#ffd046;margin-right:9px;
 vertical-align:middle;box-shadow:2px 0 #9c6a00;animation:indicatorPulse 2.2s ease-in-out infinite}
.panel-title span{font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.8px;color:#9a9e9f}
.selector-panel{margin-bottom:12px;z-index:8}
.diagnostics{animation-delay:.08s}
.selector-body{padding:10px 13px 9px}
.picker{display:flex;align-items:center}
.save-picker{flex:1;position:relative;min-width:0}
.save-display{width:100%;height:43px;position:relative;display:block;text-align:left;overflow:hidden;cursor:pointer;
 border:2px solid #5e6263;border-radius:10px;background:linear-gradient(#202324,#161819);color:#f6f6f1;
 padding:5px 51px 5px 13px;outline:none;box-shadow:inset 0 2px 5px #080909,0 2px #45494a;
 transition:border-color .16s,box-shadow .16s,transform .16s}
.save-display:hover,.save-picker.open .save-display{border-color:#ffd046;box-shadow:inset 0 2px 5px #080909,0 0 0 2px rgba(255,208,70,.16),0 2px #8d650e}
.save-display:active{transform:translateY(1px)}
.save-display:disabled{cursor:not-allowed;opacity:.42;border-color:#74402f;transform:none;
 box-shadow:inset 0 2px 5px #080909,0 2px #311611}
.save-name{display:block;color:#fff;font:12px/16px Shentox,""Arial Black"",sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.save-meta{display:block;color:#8f9595;font:10px/13px ""Inter Medium"",""Segoe UI"",sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.save-cog{position:absolute;right:0;top:0;width:39px;height:39px;border-left:1px solid #55595a;background:linear-gradient(#393d3e,#202324)}
.save-cog:before{content:'';position:absolute;left:12px;top:12px;width:11px;height:11px;border-right:3px solid #ffd046;border-bottom:3px solid #ffd046;
 transform:rotate(45deg);transition:transform .22s,top .22s}
.save-picker.open .save-cog:before{top:17px;transform:rotate(225deg)}
.save-menu{display:none;position:absolute;z-index:30;left:4px;right:4px;top:47px;max-height:245px;overflow-y:auto;
 padding:5px;background:#17191a;border:2px solid #ffd046;border-top-width:4px;border-radius:3px 3px 10px 10px;
 box-shadow:0 12px 28px rgba(0,0,0,.78),inset 0 0 0 1px #4d3d11;transform-origin:top;
 scrollbar-face-color:#3b3f40;scrollbar-track-color:#17191a;scrollbar-arrow-color:#ffd046;
 scrollbar-highlight-color:#5a5e5f;scrollbar-shadow-color:#0b0c0c;scrollbar-3dlight-color:#202324;scrollbar-darkshadow-color:#090a0a}
.save-picker.open .save-menu{display:block;animation:shutterOpen .2s cubic-bezier(.15,.78,.25,1)}
.save-option{position:relative;min-height:48px;padding:8px 12px 7px 15px;border-bottom:1px solid #353839;cursor:pointer;background:#242728;
 transition:background .13s,padding-left .13s}
.save-option:last-child{border-bottom:0}.save-option:hover{padding-left:20px;background:#34332c}
.save-option:hover:before,.save-option.active:before{content:'';position:absolute;left:5px;top:9px;bottom:9px;width:4px;background:#ffd046}
.save-option.active{background:#2f2d25}
.option-name{display:block;color:#f6f6f1;font:11px/15px Shentox,""Arial Black"",sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.option-meta{display:block;margin-top:3px;color:#939899;font:10px/13px ""Inter Medium"",""Segoe UI"",sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.save-empty{padding:13px;color:#a4a8a8;font-size:11px;text-align:center}
.picker>*+*{margin-left:9px}
.btn{height:39px;position:relative;border:2px solid #777b7c;border-radius:10px;padding:0 17px;cursor:pointer;
 color:#f5f5f2;background:linear-gradient(#3c4041,#252829);font:11px Shentox,""Arial Black"",sans-serif;
 letter-spacing:.4px;text-shadow:0 2px #000;box-shadow:inset 0 2px #55595a,0 3px 0 #101112;
 transition:transform .14s,border-color .14s,filter .14s}
.btn:hover{border-color:#fff;background:linear-gradient(#4a4e4f,#2b2e30);transform:translateY(-1px)}
.btn:active{top:2px;box-shadow:inset 0 2px #292b2c,0 1px 0 #101112}
.btn:disabled{opacity:.34;cursor:default;top:0;transform:none}
.btn-primary{min-width:124px;color:#292616;border-color:#ffd046;background:linear-gradient(#fff9b5,#ffd046 55%,#e6a91c);
 text-shadow:0 1px rgba(255,255,255,.6);box-shadow:inset 0 2px #fffbc8,0 3px 0 #9a6800;overflow:hidden}
.btn-primary:hover{border-color:#fff3a0;background:linear-gradient(#fffbd2,#ffe675 55%,#f4bd2e)}
.btn-primary:after,.btn-danger:after,.btn-patch:after{content:'';position:absolute;top:-12px;left:-45%;width:28%;height:65px;
 background:linear-gradient(90deg,transparent,rgba(255,255,255,.62),transparent);transform:skewX(-20deg)}
.btn-primary:hover:after,.btn-danger:hover:after,.btn-patch:hover:after{animation:buttonSweep .52s ease-out}
.btn-danger{height:43px;min-width:185px;color:#fff8e3;border-color:#ff6a30;background:linear-gradient(#ef5b27,#bc2e18 62%,#7e1b13);
 box-shadow:inset 0 2px #ff8754,0 3px 0 #55120c;text-shadow:0 2px #5b130d}
.btn-danger:hover{border-color:#ffbd66;background:linear-gradient(#ff7740,#d63c20 62%,#8e2115)}
.btn-patch{height:43px;min-width:185px;color:#252719;border-color:#ffe16b;background:linear-gradient(#fff7a5,#f3c637 58%,#bd8415);
 box-shadow:inset 0 2px #fffbd0,0 3px 0 #74500d;text-shadow:0 1px rgba(255,255,255,.7);overflow:hidden}
.btn-patch:hover{border-color:#fff7bd;background:linear-gradient(#fffbd0,#ffdc58 58%,#d79b20)}
.path-row{display:flex;align-items:center;margin-top:8px;color:#919696;font-size:10px}
.path-label{font:10px Shentox,""Arial Narrow"",sans-serif;color:#ffd046;letter-spacing:.65px;margin-right:9px}
.path{min-width:0;flex:1;font:10px Consolas,monospace;color:#a5aaaa;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}

.banner{position:relative;padding:8px 11px 8px 37px;margin:0 0 9px;border:1px solid;border-radius:7px;
 font-size:11px;line-height:1.45;background:#1e2021;animation:bannerDrop .25s ease-out both}
.banner:before{content:'!';position:absolute;left:12px;top:7px;width:15px;height:15px;line-height:15px;text-align:center;
 transform:rotate(45deg);font:bold 10px Arial}
.banner:after{content:'!';position:absolute;left:12px;top:7px;width:15px;height:15px;line-height:15px;text-align:center;
 color:#25221a;font:bold 10px Arial}
.banner-warn{border-color:#b67b16;color:#e7c06e}.banner-warn:before{background:#ffd046}
.banner-error{border-color:#b93b29;color:#ff9c81}.banner-error:before{background:#f0502b}
.banner-good{border-color:#4d9b8a;color:#9ad8ca}.banner-good:before{background:#69c7ad}

.diagnostics{margin-top:0}
.diagnostic-body{padding:12px 13px 14px}
.stats{display:flex;margin:-4px -4px 8px}
.stat{flex:1;min-width:0;height:67px;margin:4px;padding:9px 10px;background:#202b2f;border:1px solid #3c555e;
 border-radius:3px 12px 3px 12px;box-shadow:inset 0 0 18px rgba(18,104,135,.12),0 2px #121415;
 transition:transform .16s,border-color .16s;animation:dataBoot .3s ease-out both}
.stat:nth-child(2){animation-delay:.04s}.stat:nth-child(3){animation-delay:.08s}.stat:nth-child(4){animation-delay:.12s}.stat:nth-child(5){animation-delay:.16s}
.stat:hover{transform:translateY(-2px);border-color:#6a93a1}
.stat .label{font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1px;color:#8eb4c1;text-transform:uppercase}
.stat .value{margin-top:5px;font:18px Shentox,""Arial Black"",sans-serif;color:#f3f3ed;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.stat .value.small{font-size:14px;margin-top:7px}.stat .value.ok{color:#ffd046}.stat .value.bad{color:#ff633d}.stat .value.accent{color:#ffb52f}

.raid{position:relative;margin-top:11px;background:#242729;border:2px solid #99321f;border-radius:8px 18px 8px 18px;
 box-shadow:inset 0 0 0 2px #351712,inset 0 1px #4b4e4f,0 4px 10px rgba(0,0,0,.38);overflow:hidden;
 animation:raidDock .38s cubic-bezier(.15,.82,.28,1) both}
.raid:before,.raid:after{content:'';position:absolute;z-index:2;width:7px;height:7px;border-radius:50%;
 background:#777b7c;border:2px solid #101112;box-shadow:inset 0 1px #b6b9b9}
.raid:before{left:6px;top:6px}.raid:after{right:6px;bottom:6px}
.raid-head{min-height:67px;padding:10px 16px 10px 17px;background:linear-gradient(90deg,#321713,#242729 43%);
 border-bottom:1px solid #0b0c0c;display:flex;align-items:center}
.tier-badge{width:58px;height:58px;flex:0 0 58px;margin:0 10px 0 0;overflow:visible}
.tier-badge svg{display:block;width:58px;height:58px;overflow:visible}
.tier-shape{fill:#32100c;stroke:#ff6338;stroke-width:3}
.tier-number{fill:#fff2dd;font-family:Shentox,""Arial Black"",sans-serif;font-size:22px;text-anchor:middle}
.raid-name{min-width:185px}.raid-name h4{margin:0;font:14px Shentox,""Arial Black"",sans-serif;letter-spacing:.45px}
.raid-name p{margin:4px 0 0;color:#b4b8b8;font:10px Consolas,monospace}
.raid-meter-wrap{flex:1;margin:0 18px}.meter-label{display:flex;justify-content:space-between;margin-bottom:5px;
 color:#a5aaaa;font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.8px}
.raid-meter{height:12px;padding:2px;background:#111314;border:1px solid #050606;border-radius:4px;display:flex;box-shadow:inset 0 1px 3px #000}
.meter-seg{flex:1;margin-right:2px;background:#3b3d3e;border-bottom:1px solid #151616}.meter-seg:last-child{margin-right:0}
.meter-seg.on{animation:meterCharge .38s ease-out backwards}.meter-seg:nth-child(2){animation-delay:.08s}.meter-seg:nth-child(3){animation-delay:.16s}
.meter-seg:nth-child(4){animation-delay:.24s}.meter-seg:nth-child(5){animation-delay:.32s}
.meter-seg.on.s1{background:linear-gradient(#ffe765,#ffd332)}.meter-seg.on.s2{background:linear-gradient(#ffc34a,#f6a929)}
.meter-seg.on.s3{background:linear-gradient(#ff9835,#ed6f1f)}.meter-seg.on.s4{background:linear-gradient(#f66a2c,#db3d19)}
.meter-seg.on.s5{background:linear-gradient(#f43523,#bd1010)}.meter-seg.super{background:linear-gradient(#8368e7,#4d36a7)}
.state{max-width:150px;color:#d6d7d4;background:#1b1d1e;border:1px solid #5c6061;padding:6px 9px;border-radius:8px;
 font:10px Shentox,""Arial Narrow"",sans-serif;text-align:center;animation:stateBlink 2.8s ease-in-out infinite}
.raid-body{padding:11px 16px 14px}
.mini-grid{display:flex;flex-wrap:wrap;margin:-3px}
.mini{width:20%;padding:3px}.mini>div{height:50px;padding:8px 9px;background:#191b1c;border:1px solid #3e4243;
 border-radius:2px 9px 2px 9px;box-shadow:inset 0 1px #0d0e0e}
.mini span{display:block;color:#969b9b;font:9px Shentox,""Arial Narrow"",sans-serif;text-transform:uppercase;letter-spacing:.55px}
.mini strong{display:block;margin-top:4px;color:#ffd046;font:12px Shentox,""Arial Black"",sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.section-label{height:23px;margin:10px 0 5px;padding:6px 8px 0;color:#f6f6f1;background:#1d1f20;border-left:4px solid #ffd046;
 font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.9px;text-transform:uppercase}
.chips{display:flex;flex-wrap:wrap;margin:-3px}
.chip{margin:3px;padding:6px 8px;background:#35322a;border:1px solid #a0731d;border-radius:7px 2px 7px 2px;
 color:#e9e6db;font-size:10px;box-shadow:inset 0 1px #554b32;transition:transform .14s,border-color .14s,background .14s}
.chip:hover{transform:translateY(-2px);border-color:#ffd046;background:#413b2d}
.chip b{color:#ffd046;margin-left:4px;font-family:Shentox,""Arial Black"",sans-serif}
.chip.crop{background:#20343a;border-color:#356f82;box-shadow:inset 0 1px #35515a}.chip.crop b{color:#7cd7ee}
.chip.raw{background:#232627;border-color:#4b5051;color:#aeb2b1}.chip.raw b{color:#f0bd3b}
.note{position:relative;margin-top:7px;padding:8px 9px 8px 31px;color:#e5ba64;background:#302a1f;border:1px solid #745319;
 border-radius:3px;font-size:10px;line-height:1.5}.note:before{content:'!';position:absolute;left:10px;top:8px;color:#ffd046;font:bold 13px Arial;
 animation:warningBlink 1.8s ease-in-out infinite}
.repair-bar{display:flex;align-items:center;justify-content:space-between;margin-top:12px;padding:10px 0 1px;border-top:1px solid #4a4d4e}
.repair-bar p{max-width:455px;margin:0;color:#a0a5a5;font-size:10px;line-height:1.55}
.repair-bar p b{color:#ffd046;font-family:Shentox,""Arial Narrow"",sans-serif;letter-spacing:.5px}
.repair-actions{display:flex;align-items:center;white-space:nowrap}.repair-actions .btn+.btn{margin-left:9px}
.empty{text-align:center;padding:36px 15px 40px;color:#8b9090}
.empty .diamond{width:34px;height:34px;margin:0 auto 19px;transform:rotate(45deg);border:3px solid #ffd046;border-radius:5px;background:#272a2b}
.empty .diamond span{display:block;transform:rotate(-45deg);font:bold 18px/28px Arial;color:#ffd046}
.empty h4{margin:0 0 5px;color:#f6f6f2;font:12px Shentox,""Arial Black"",sans-serif;letter-spacing:.5px}
.empty p{margin:0;font-size:11px}
.success-box{padding:16px;background:#202b2f;border:1px solid #3c7581;border-radius:4px 13px 4px 13px}
.success-box .backup-label{font:10px Shentox,""Arial Narrow"",sans-serif;color:#ffd046;letter-spacing:.8px;margin-bottom:7px}
.perf-zone{margin-top:14px;padding-top:12px;border-top:1px solid #4a4d4e}
.perf-shell{overflow:hidden;background:#1c2224;border:1px solid #55747a;border-left:5px solid #ffd046;
 border-radius:4px 16px 4px 16px;box-shadow:inset 0 1px #344246,0 3px 9px rgba(0,0,0,.35)}
.perf-head{display:flex;align-items:center;justify-content:space-between;padding:12px 14px;
 background:linear-gradient(100deg,#273018,#1d2b2f 55%,#173943);border-bottom:1px solid #0d1112}
.perf-title b{display:block;color:#ffd046;font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1.2px}
.perf-title strong{display:block;margin-top:3px;color:#f5f6f1;font:15px Shentox,""Arial Black"",sans-serif;letter-spacing:.55px}
.perf-title p{margin:5px 0 0;color:#aab4b4;font-size:10px;line-height:1.45}.perf-head .btn{min-width:170px;margin-left:18px}
.perf-body{padding:12px 14px 14px}.perf-progress-card{padding:12px;background:#151b1d;border:1px solid #41626a;border-radius:3px 12px 3px 12px}
.perf-progress-top{display:flex;justify-content:space-between;align-items:flex-start}.perf-progress-copy b{display:block;color:#82dff2;
 font:11px Shentox,""Arial Black"",sans-serif;letter-spacing:.45px}.perf-progress-copy span{display:block;margin-top:4px;color:#929e9f;font-size:9px}
.perf-progress-percent{color:#ffd046;font:20px Shentox,""Arial Black"",sans-serif}.perf-track{height:12px;margin-top:10px;padding:2px;
 background:#090c0d;border:1px solid #34474b;border-radius:4px;overflow:hidden}.perf-fill{height:6px;background:linear-gradient(90deg,#3a8798,#6fd7ed 60%,#ffd046);
 box-shadow:0 0 8px rgba(105,204,229,.38);transition:width .18s ease-out}
.perf-stages{display:flex;margin:10px -2px 0}.perf-stage{flex:1;min-width:0;height:25px;margin:0 2px;padding:7px 3px 0;
 color:#667174;background:#22282a;border:1px solid #343d3f;text-align:center;font:7px Shentox,""Arial Narrow"",sans-serif;
 letter-spacing:.25px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.perf-stage.done{color:#b7ecf5;border-color:#438797;background:#18343a}
.perf-stage.current{color:#28220e;border-color:#ffd046;background:#eab82c;box-shadow:0 0 8px rgba(255,208,70,.22)}
.perf-cancel-row{display:flex;align-items:center;justify-content:space-between;margin-top:11px;color:#899495;font-size:9px}
.perf-cancel{height:33px;min-width:128px;color:#ffc3b2;border-color:#9d3e2b;background:linear-gradient(#5d261b,#35140f)}
.perf-summary{display:flex;margin:-4px -4px 9px}.perf-stat{flex:1;min-width:0;margin:4px;padding:9px 8px;background:#172225;
 border:1px solid #3d646d;border-radius:3px 10px 3px 10px}.perf-stat b{display:block;color:#79d9ee;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.65px}
.perf-stat strong{display:block;margin-top:4px;color:#fff;font:15px Shentox,""Arial Black"",sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.perf-columns{display:flex;margin:0 -5px}.perf-column{width:50%;padding:0 5px}.perf-subtitle{margin:3px 0 7px;color:#ffd046;
 font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.8px}.perf-world,.perf-category{display:flex;align-items:center;justify-content:space-between;
 min-height:38px;margin-top:5px;padding:7px 9px;background:#151a1b;border:1px solid #374649;border-radius:2px 8px 2px 8px}
.perf-world b,.perf-category b{display:block;color:#eef1ed;font:10px Shentox,""Arial Black"",sans-serif}.perf-world span,.perf-category span{display:block;margin-top:2px;color:#7f8c8e;font-size:8px}
.perf-world strong{color:#75d5ea;font:11px Consolas,monospace}.perf-category-copy{min-width:0;flex:1}.perf-category-value{margin-left:8px;color:#ffd046;font:10px Consolas,monospace}
.perf-category-bar{height:4px;margin-top:5px;background:#263033}.perf-category-bar i{display:block;height:4px;background:linear-gradient(90deg,#4a9bae,#7ee1f3)}
.perf-limit{margin-top:10px;padding:9px 10px;color:#a3acad;background:#27271e;border:1px solid #6b5b25;border-radius:3px;font-size:9px;line-height:1.5}
.perf-message{padding:15px;color:#aeb7b7;background:#151a1b;border:1px solid #3e4b4e;border-radius:3px;font-size:10px;line-height:1.5}
.perf-message b{display:block;margin-bottom:5px;color:#fff;font:12px Shentox,""Arial Black"",sans-serif}.perf-message.bad{color:#ffab94;border-color:#8f3826}
.perf-filters{display:flex;align-items:center;margin:11px 0 7px;padding:7px;background:#121718;border:1px solid #354346;
 border-radius:3px}.perf-filter-label{margin:0 9px 0 3px;color:#798587;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.7px}
.perf-filter{height:29px;margin-right:5px;padding:0 10px;color:#a9b3b4;background:#202729;border:1px solid #3d4b4e;
 border-radius:2px 7px 2px 7px;cursor:pointer;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.45px}
.perf-filter:hover{color:#e9fbff;border-color:#5caabe}.perf-filter.active{color:#171b1c;background:#71d8ed;border-color:#9ceafa;box-shadow:inset 0 1px #d6f9ff}
.perf-hotspot-list{margin-top:8px}.perf-hotspot-empty{padding:23px 15px;color:#879294;background:#151a1b;border:1px dashed #465356;
 text-align:center;font-size:10px}.perf-hotspot-empty b{display:block;margin-bottom:5px;color:#dce3e1;font:12px Shentox,""Arial Black"",sans-serif}
.perf-hotspot{position:relative;margin-top:9px;overflow:hidden;background:linear-gradient(100deg,#1b2224,#202526);
 border:1px solid #566064;border-left:5px solid #d1a42b;border-radius:4px 15px 4px 15px;
 box-shadow:inset 0 1px #343b3d,0 3px 8px rgba(0,0,0,.28);animation:perfCardIn .3s ease both}
.perf-hotspot.heavy{border-left-color:#f0832d}.perf-hotspot.very-heavy{border-left-color:#f04e2b;box-shadow:inset 0 1px #4a3733,0 3px 10px rgba(126,35,20,.28)}
.perf-hotspot-head{display:flex;align-items:center;padding:10px 12px;background:rgba(8,12,13,.34);border-bottom:1px solid #343e40}
.perf-rank{display:flex;align-items:center;justify-content:center;flex:0 0 42px;width:42px;height:42px;margin-right:11px;color:#24200f;
 background:#ffd046;border:2px solid #fff0a0;transform:rotate(45deg);font:13px Shentox,""Arial Black"",sans-serif;box-shadow:0 0 0 2px #151718}
.perf-rank span{transform:rotate(-45deg)}.perf-hotspot-title{min-width:0;flex:1}.perf-hotspot-title b{display:block;color:#fff;
 font:13px Shentox,""Arial Black"",sans-serif;letter-spacing:.35px}.perf-hotspot-title span{display:block;margin-top:4px;color:#75d6eb;font-size:9px}
.perf-severity{min-width:94px;padding:7px 8px;color:#2b220b;background:#d5ab32;border:1px solid #ffe07b;text-align:center;
 font:9px Shentox,""Arial Black"",sans-serif;letter-spacing:.5px}.perf-severity.heavy{color:#301407;background:#ef8a31;border-color:#ffc078}
.perf-severity.very-heavy{color:#fff2eb;background:#c94325;border-color:#ff7857;box-shadow:0 0 8px rgba(231,75,39,.32)}
.perf-confidence{margin-left:7px;padding:6px 7px;color:#8de8f7;background:#15333a;border:1px solid #397886;
 font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.45px}.perf-hotspot-body{padding:11px 12px 12px}
.perf-hotspot-metrics{display:flex;margin:0 -3px 9px}.perf-hotspot-metric{flex:1;min-width:0;margin:0 3px;padding:7px 8px;
 background:#151a1b;border:1px solid #394649;border-radius:2px 8px 2px 8px}.perf-hotspot-metric b{display:block;color:#7d898b;
 font:7px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.55px}.perf-hotspot-metric strong{display:block;margin-top:3px;color:#f4f5f1;
 font:11px Consolas,monospace;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.perf-hotspot-metric.coordinate strong{color:#7bddf0}
.perf-hotspot-compare{padding:8px 9px;color:#c2cece;background:#19292d;border:1px solid #386875;border-radius:2px;font-size:9px}
.perf-hotspot-compare b{color:#82e2f4;font-family:Shentox,""Arial Narrow"",sans-serif}.perf-evidence{display:flex;flex-wrap:wrap;margin:6px -3px 0}
.perf-evidence-item{width:50%;padding:3px}.perf-evidence-item>div{min-height:57px;padding:8px 9px;background:#181c1d;border:1px solid #3b4547;
 border-left:3px solid #d5aa32;border-radius:2px 8px 2px 8px}.perf-evidence-item b{display:block;color:#f0f2ed;font:9px Shentox,""Arial Black"",sans-serif}
.perf-evidence-item span{display:block;margin-top:4px;color:#8e999a;font-size:8px;line-height:1.4}
.perf-evidence-item em{display:block;margin-top:5px;color:#d7b64f;font:normal 7px Consolas,monospace;letter-spacing:.25px}.perf-hotspot-foot{display:flex;align-items:flex-end;
 justify-content:space-between;margin-top:7px;padding-top:7px;border-top:1px solid #343d3f}.perf-hotspot-category{min-width:260px;max-width:470px;flex:1}
.perf-hotspot-category b{display:block;color:#9ba5a5;font:8px Shentox,""Arial Narrow"",sans-serif}.perf-hotspot-category span{display:block;margin-top:3px;color:#727e80;font-size:8px}
.perf-hotspot-category-line{height:5px;margin-top:5px;background:#2a3234}.perf-hotspot-category-line i{display:block;height:5px;background:linear-gradient(90deg,#53aabe,#7de1f3)}
.perf-copy{height:31px;min-width:150px;margin-left:12px;color:#d6f8ff;border-color:#4c98a9;background:linear-gradient(#28515a,#19343a)}
.perf-copy.copied{color:#16280e;border-color:#9ccd4b;background:linear-gradient(#c9ed75,#8fbd3e)}
.perf-report-tools{display:flex;align-items:center;margin:9px 0;padding:9px 10px;background:#151b1d;border:1px solid #3b5960;
 border-radius:3px 11px 3px 11px}.perf-tool-copy{min-width:0;flex:1}.perf-tool-copy b{display:block;color:#f2f4ef;
 font:9px Shentox,""Arial Black"",sans-serif;letter-spacing:.5px}.perf-tool-copy span{display:block;margin-top:3px;color:#849193;font-size:8px;line-height:1.4}
.perf-tool-actions{display:flex;align-items:center;margin-left:12px}.perf-tool-actions .btn{height:31px;min-width:128px;margin-left:6px}
.perf-export{color:#fff1bd;border-color:#9c7a24;background:linear-gradient(#66501d,#3d2f11)}.perf-explore{color:#cff7ff;border-color:#3f8999;background:linear-gradient(#26515a,#17353b)}
.perf-tool-status{margin-top:5px;color:#8fe6f5;font-size:8px}.perf-tool-status.bad{color:#ff9d84}
.perf-explorer{margin:9px 0 11px;overflow:hidden;background:#111719;border:1px solid #4a7882;border-left:4px solid #65cce2;
 border-radius:3px 12px 3px 12px}.perf-explorer-head{display:flex;align-items:center;justify-content:space-between;padding:10px 11px;
 background:linear-gradient(90deg,#17343a,#202728);border-bottom:1px solid #35535a}.perf-explorer-head b{display:block;color:#7de0f3;
 font:10px Shentox,""Arial Black"",sans-serif;letter-spacing:.6px}.perf-explorer-head span{display:block;margin-top:3px;color:#879597;font-size:8px}
.perf-explorer-worlds{display:flex;flex-wrap:wrap;padding:7px 8px 2px}.perf-explorer-world{height:27px;margin:0 5px 5px 0;padding:0 9px;
 color:#a6b3b5;background:#202729;border:1px solid #3d5054;cursor:pointer;font:8px Shentox,""Arial Narrow"",sans-serif}
.perf-explorer-world.active{color:#132226;background:#72d8eb;border-color:#a7effb}.perf-cell-list{padding:3px 8px 7px}
.perf-cell{display:flex;align-items:center;margin-top:5px;padding:7px 9px;background:#192023;border:1px solid #35474b;border-radius:2px 8px 2px 8px}
.perf-cell-coordinate{width:145px;flex:0 0 145px}.perf-cell-coordinate b{display:block;color:#80dff1;font:10px Consolas,monospace}
.perf-cell-coordinate span,.perf-cell-categories span{display:block;margin-top:2px;color:#748184;font-size:7px}.perf-cell-metric{width:112px;flex:0 0 112px}
.perf-cell-metric b{display:block;color:#f1f3ee;font:10px Consolas,monospace}.perf-cell-metric span{display:block;margin-top:2px;color:#758184;font-size:7px}
.perf-cell-categories{min-width:0;flex:1}.perf-cell-categories b{display:block;color:#d6dcda;font:8px Shentox,""Arial Narrow"",sans-serif;
 white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.perf-explorer-page{display:flex;align-items:center;justify-content:space-between;padding:8px 9px;
 color:#839092;background:#171d1f;border-top:1px solid #35464a;font-size:8px}.perf-explorer-page .btn{height:28px;min-width:92px;margin-left:6px}
.drop-zone{margin-top:14px;border-top:1px solid #4a4d4e;padding-top:12px}
.drop-zone-head{display:flex;align-items:center;justify-content:space-between;padding:10px 12px;
 background:linear-gradient(100deg,#17333d,#222728 58%,#352c18);border:1px solid #407384;border-left:5px solid #69cce5;
 border-radius:4px 14px 4px 14px;box-shadow:inset 0 1px #31515b,0 2px #121415}
.drop-zone-title{min-width:0}.drop-zone-title b{display:block;color:#76d5eb;font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1.2px}
.drop-zone-title strong{display:block;margin-top:3px;color:#f5f6f1;font:15px Shentox,""Arial Black"",sans-serif;letter-spacing:.55px}
.drop-zone-summary{display:flex;align-items:center;margin-left:14px}.drop-count{padding:6px 9px;margin-right:8px;color:#c4cbca;background:#16191a;
 border:1px solid #495557;border-radius:3px;font-size:9px}.drop-count b{color:#ffd046;font:11px Shentox,""Arial Black"",sans-serif}
.drop-collapse{display:flex;align-items:center;justify-content:center;flex:0 0 31px;width:31px;height:31px;margin-right:8px;padding:0;
 color:#bdecf5;background:linear-gradient(#293437,#171b1c);border:1px solid #54818b;border-radius:3px 9px 3px 9px;
 box-shadow:inset 0 1px #435256,0 2px #0d1011;cursor:pointer;transition:border-color .14s,background .14s,transform .14s}
.drop-collapse:hover{color:#fff;border-color:#82dff2;background:linear-gradient(#365057,#1c272a);transform:translateY(-1px)}
.drop-collapse:active{transform:translateY(1px)}.drop-collapse svg{display:block;width:17px;height:17px;overflow:visible;
 transition:transform .18s ease}.drop-collapse path{fill:none;stroke:currentColor;stroke-width:2.4;stroke-linecap:square;stroke-linejoin:miter}
.drop-collapse.is-collapsed svg{transform:rotate(180deg)}.drop-items-body[hidden]{display:none}
.drop-zone-summary .btn+.btn{margin-left:8px}.btn-expired{height:43px;min-width:174px;color:#30260d;border-color:#e6ae24;
 background:linear-gradient(#ffe681,#eab62d 58%,#a66b12);box-shadow:inset 0 2px #fff3aa,0 3px 0 #65420c;
 text-shadow:0 1px rgba(255,255,255,.45);overflow:hidden}.btn-expired:hover{border-color:#fff0a0;background:linear-gradient(#fff0a0,#f7c842 58%,#bd7c18)}
.btn-expired:disabled{color:#8f8a7a;border-color:#59574e;background:#353633;box-shadow:inset 0 1px #52534f,0 3px #171816;text-shadow:none}
.btn-summary{height:43px;min-width:126px;color:#c8f5ff;border-color:#4faec4;background:linear-gradient(#285b68,#1b3c44 62%,#12292f);
 box-shadow:inset 0 2px #3f7885,0 3px #0a171a}.btn-summary:hover{color:#fff;border-color:#8ce6f8;background:linear-gradient(#347182,#214b56 62%,#15343b)}
.drop-scan-panel{position:relative;margin-top:10px;padding:19px 210px 19px 20px;min-height:92px;overflow:hidden;
 background:linear-gradient(105deg,#1d2426,#202829 60%,#173541);border:1px solid #46646b;border-left:5px solid #69cce5;
 border-radius:4px 16px 4px 16px;box-shadow:inset 0 1px #344246,0 3px 9px rgba(0,0,0,.35)}
.drop-scan-panel:before{content:'';position:absolute;right:176px;top:-35px;width:86px;height:150px;transform:rotate(22deg);
 background:rgba(99,211,235,.06);border-left:1px solid rgba(112,222,244,.18)}
.drop-scan-panel b{display:block;color:#f4f7f3;font:14px Shentox,""Arial Black"",sans-serif;letter-spacing:.55px}
.drop-scan-panel p{margin:7px 0 0;color:#aab4b4;font-size:10px;line-height:1.5}.drop-scan-panel .btn{position:absolute;right:18px;top:24px;width:170px}
.drop-grid{display:flex;flex-wrap:wrap;margin:5px -5px -5px}
.drop-wrap{width:50%;padding:5px;animation:dropDock .3s cubic-bezier(.16,.83,.3,1) both}
.drop-wrap:nth-child(2){animation-delay:.035s}.drop-wrap:nth-child(3){animation-delay:.07s}.drop-wrap:nth-child(4){animation-delay:.105s}
.drop-card{position:relative;min-height:145px;padding:12px 12px 9px 105px;overflow:hidden;background:#202426;
 border:1px solid #536064;border-top:3px solid #54b7d0;border-radius:5px 15px 5px 15px;
 box-shadow:inset 0 1px #3a4143,0 3px 9px rgba(0,0,0,.34);transition:border-color .15s,transform .15s}
.drop-card:hover{border-color:#79d9ee;transform:translateY(-2px)}.drop-card:after{content:'';position:absolute;right:5px;bottom:5px;
 width:6px;height:6px;border-right:2px solid #718084;border-bottom:2px solid #718084}
.drop-icon-frame{position:absolute;left:13px;top:13px;width:78px;height:78px;padding:5px;background:linear-gradient(145deg,#384044,#171a1b);
 border:2px solid #d8a322;border-radius:5px 16px 5px 16px;box-shadow:inset 0 0 0 2px #17191a,0 3px #0c0d0e}
.drop-icon{display:block;width:64px;height:64px;object-fit:contain}.drop-icon-fallback{display:flex;align-items:center;justify-content:center;
 width:64px;height:64px;color:#ffd046;background:#252b2d;font:24px Shentox,""Arial Black"",sans-serif}
.drop-quantity{position:absolute;right:-6px;bottom:-7px;min-width:27px;height:25px;padding:0 6px;color:#27210c;text-align:center;
 background:#ffd046;border:2px solid #161819;border-radius:10px 3px 10px 3px;font:12px/21px Shentox,""Arial Black"",sans-serif;
 box-shadow:0 0 0 1px #d79b18}
.drop-copy{min-width:0;padding-right:3px}.drop-kind{display:block;color:#6fcce3;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1px;text-transform:uppercase}
.drop-name{display:block;margin-top:3px;color:#fff;font:13px/16px Shentox,""Arial Black"",sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.drop-description{height:29px;margin:4px 0 6px;color:#aeb5b4;font-size:9px;line-height:1.45;overflow:hidden}
.drop-life{display:inline-block;padding:4px 7px;color:#c7e7ee;background:#17272c;border:1px solid #386d7b;border-radius:7px 2px 7px 2px;
 font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.45px}.drop-life.soon{color:#ffd090;background:#332718;border-color:#a46d18}
.drop-life.expired{color:#ffb29d;background:#351915;border-color:#9e3a26}
.drop-detail-row{display:flex;flex-wrap:wrap;margin:7px -2px 0}.drop-detail{margin:2px;padding:4px 6px;color:#969e9e;background:#171a1b;
 border:1px solid #3b4244;border-radius:2px;font-size:8px}.drop-detail b{color:#e8eae6;font-family:Consolas,monospace;font-weight:normal}
.drop-detail.drop-value{border-color:#80631c;background:#292413}.drop-detail.drop-value b{color:#ffd046;font-family:Shentox,""Arial Black"",sans-serif}
.drop-remove{position:absolute;left:13px;top:101px;width:78px;height:31px;padding:0;color:#ffd4c8;background:linear-gradient(#692518,#44150f);
 border:1px solid #c64b2e;border-radius:4px 9px 4px 9px;cursor:pointer;font:8px Shentox,""Arial Black"",sans-serif;letter-spacing:.35px;
 box-shadow:inset 0 1px #9a3925,0 2px #100706;text-shadow:0 1px #210604}
.drop-remove:hover{color:#fff3dc;border-color:#ff8b61;background:linear-gradient(#8b321f,#57190f)}
.drop-remove:disabled{cursor:default;opacity:.35}.drop-empty{padding:20px 12px 8px;text-align:center;color:#7f8889;font-size:10px}
.drop-warning{margin-top:9px;padding:8px 10px;color:#d5aa5e;background:#2d271b;border:1px solid #6f521b;border-radius:3px;font-size:9px;line-height:1.45}
.item-clear-modal{z-index:190}.item-clear-modal .hotfix-dialog{width:650px;border-color:#ef5932}
.item-confirm-preview{display:flex;align-items:center;margin:0 0 11px;padding:10px 11px;background:#171a1b;border:1px solid #4b5355;border-radius:4px 13px 4px 13px}
.item-confirm-icon{position:relative;box-sizing:border-box;width:66px;height:66px;min-width:66px;max-width:66px;min-height:66px;max-height:66px;
 flex:0 0 66px;margin-right:13px;padding:4px;background:#282e30;border:2px solid #d9a31f;border-radius:4px 13px 4px 13px}
.item-confirm-icon img{display:block;width:54px;height:54px;object-fit:contain}.item-confirm-fallback{position:absolute;left:4px;right:4px;top:4px;bottom:4px;
 width:auto;height:auto;color:#ffd046;text-align:center;font:20px/54px Shentox,""Arial Black"",sans-serif;white-space:nowrap}
.item-confirm-copy{min-width:0}.item-confirm-copy b{display:block;color:#fff;font:14px Shentox,""Arial Black"",sans-serif}
.item-confirm-copy span{display:block;margin-top:5px;color:#9ca4a4;font-size:10px;line-height:1.4}.item-clear-modal .hotfix-confirm{min-width:214px}
.item-summary-modal{z-index:195;background:radial-gradient(circle at 50% 38%,rgba(38,135,157,.3),rgba(4,6,7,.95) 67%)}
.item-summary-modal .hotfix-dialog{width:720px;border-color:#69cce5;box-shadow:inset 0 0 0 2px #214d57,0 22px 70px #000}
.item-summary-modal .hotfix-hazard{background:repeating-linear-gradient(135deg,#69cce5 0,#69cce5 18px,#1a1c1d 18px,#1a1c1d 36px)}
.item-summary-modal .hotfix-head{background:linear-gradient(90deg,#173d47,#292b2c 58%,#3b321b)}
.item-summary-modal .hotfix-title strong{color:#8ce6f8}.item-summary-emblem{width:58px;height:58px;min-width:58px;max-width:58px;min-height:58px;max-height:58px;
 flex:0 0 58px;margin-right:16px}.item-summary-emblem svg{display:block;width:58px;height:58px;overflow:visible}
.item-summary-emblem polygon{fill:#142b31;stroke:#69cce5;stroke-width:3;stroke-linejoin:round;filter:drop-shadow(0 0 2px #081012)}
.item-summary-emblem text{fill:#ffd046;text-anchor:middle;font:19px Shentox,""Arial Black"",sans-serif}
.item-summary-stats{display:flex;margin:0 -4px 10px}.item-summary-stat{flex:1;margin:0 4px;padding:9px 11px;background:#171b1c;border:1px solid #46575b;border-radius:3px 10px 3px 10px}
.item-summary-stat b{display:block;color:#74d5eb;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.8px}
.item-summary-stat strong{display:block;margin-top:3px;color:#fff;font:17px Shentox,""Arial Black"",sans-serif}
.item-summary-scroll-shell{position:relative;height:390px;padding-right:18px;overflow:hidden}.item-summary-list{display:flex;flex-wrap:wrap;align-content:flex-start;height:390px;
 margin:-3px;overflow-y:scroll;overflow-x:hidden;-ms-overflow-style:none;scrollbar-width:none}.item-summary-list::-webkit-scrollbar{width:0;height:0}
.item-summary-row{display:flex;align-items:center;width:calc(50% - 6px);min-height:70px;margin:3px;padding:7px 9px;
 background:linear-gradient(90deg,#1b2021,#222728);border:1px solid #485356;border-left:4px solid #69cce5;border-radius:3px 12px 3px 12px}
.item-summary-icon{box-sizing:border-box;width:54px;height:54px;min-width:54px;max-width:54px;min-height:54px;max-height:54px;flex:0 0 54px;
 margin-right:10px;padding:4px;background:#272d2f;border:2px solid #a77d1a;border-radius:3px 10px 3px 10px;overflow:hidden}
.item-summary-icon img{display:block;width:42px;height:42px;object-fit:contain}.item-summary-icon span{display:flex;align-items:center;justify-content:center;width:42px;height:42px;color:#ffd046;font:17px Shentox,""Arial Black"",sans-serif}
.item-summary-copy{min-width:0;flex:1}.item-summary-copy b{display:block;color:#fff;font:12px Shentox,""Arial Black"",sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.item-summary-copy span{display:block;margin-top:4px;color:#829193;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.45px}
.item-summary-amount{width:52px;flex:0 0 52px;text-align:right;margin-left:7px}.item-summary-amount strong{display:block;color:#ffd046;font:18px Shentox,""Arial Black"",sans-serif}
.item-summary-amount span{display:block;margin-top:3px;color:#aeb5b4;font-size:8px}.item-summary-empty{padding:28px;text-align:center;color:#899293;background:#171a1b;border:1px solid #41494b}
.item-summary-scroll-track{position:absolute;right:1px;top:0;bottom:0;width:13px;background:#101516;border:1px solid #42555a;
 box-shadow:inset 0 0 0 2px #1d292c;border-radius:2px}.item-summary-scroll-track:before,.item-summary-scroll-track:after{content:'';position:absolute;left:3px;width:0;height:0;border-left:3px solid transparent;border-right:3px solid transparent}
.item-summary-scroll-track:before{top:4px;border-bottom:5px solid #7bcfe2}.item-summary-scroll-track:after{bottom:4px;border-top:5px solid #7bcfe2}
.item-summary-scroll-thumb{position:absolute;left:2px;top:15px;width:7px;min-height:38px;background:linear-gradient(90deg,#4ba9be,#8ce6f8 55%,#39869a);
 border:1px solid #baf4ff;box-shadow:0 0 7px rgba(105,204,229,.3);cursor:default}
.item-summary-scroll-thumb:before{content:'';position:absolute;left:1px;right:1px;top:50%;height:2px;margin-top:-1px;background:#1c5663;border-top:1px solid #d5faff}
.item-summary-scroll-thumb.disabled{opacity:.2;box-shadow:none}
.footer{text-align:center;margin-top:10px;color:#777c7c;font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.55px}

.hotfix-modal{display:none;position:fixed;top:0;right:0;bottom:0;left:0;z-index:24;padding:22px;
 background:radial-gradient(circle at 50% 40%,rgba(118,27,13,.27),rgba(4,5,5,.94) 64%);align-items:center;justify-content:center}
.hotfix-modal.show{display:flex;animation:hotfixBackdrop .2s ease-out both}
.hotfix-dialog{position:relative;width:560px;max-width:100%;overflow:hidden;background:#242729;border:2px solid #e34825;
 border-radius:8px 22px 8px 22px;box-shadow:inset 0 0 0 2px #511b13,0 22px 70px #000;
 animation:hotfixDeploy .3s cubic-bezier(.16,.82,.28,1) both}
.hotfix-hazard{height:13px;border-bottom:2px solid #0c0d0d;
 background:repeating-linear-gradient(135deg,#ffd046 0,#ffd046 18px,#1a1c1d 18px,#1a1c1d 36px);
 background-size:51px 51px;animation:hazardMove 1.1s linear infinite}
.hotfix-head{display:flex;align-items:center;padding:16px 19px 14px;background:linear-gradient(90deg,#461a13,#292b2c 58%);border-bottom:1px solid #090a0a}
.hotfix-alert{width:47px;height:47px;flex:0 0 47px;margin-right:16px;position:relative}
.hotfix-alert:before{content:'';position:absolute;left:50%;top:50%;width:30px;height:30px;margin:-15px 0 0 -15px;transform:rotate(45deg);
 box-sizing:border-box;background:#38100b;border:3px solid #ff5c35;border-radius:4px;box-shadow:0 0 0 2px #170907,0 0 15px rgba(255,73,39,.35);
 animation:dangerPulse 1.45s ease-in-out infinite}
.hotfix-alert span{position:absolute;z-index:2;left:50%;top:50%;width:30px;height:30px;margin:-15px 0 0 -15px;font-size:0}
.hotfix-alert span:before,.hotfix-alert span:after{content:'';position:absolute;left:13px;width:4px;background:#fff1d8;
 box-shadow:0 2px #61160d}
.hotfix-alert span:before{top:5px;height:13px;border-radius:2px 2px 1px 1px}
.hotfix-alert span:after{top:21px;height:4px;border-radius:50%}
.hotfix-title{min-width:0}.hotfix-title strong{display:block;color:#ff7654;font:15px Shentox,""Arial Black"",sans-serif;letter-spacing:.75px}
.hotfix-title span{display:block;margin-top:5px;color:#d7d8d4;font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.8px}
.hotfix-body{padding:15px 19px 16px}.hotfix-intro{margin:0 0 11px;color:#d4d6d4;font-size:12px;line-height:1.5}
.hotfix-checks{margin:0;padding:0;list-style:none;border:1px solid #464a4b;background:#1a1c1d}
.hotfix-checks li{position:relative;padding:8px 10px 8px 34px;border-bottom:1px solid #343738;color:#bfc3c2;font-size:11px;line-height:1.35}
.hotfix-checks li:last-child{border-bottom:0}.hotfix-checks li:before{content:'✓';position:absolute;left:11px;top:7px;color:#ffd046;
 font:bold 13px Arial;text-shadow:0 0 7px rgba(255,208,70,.35)}
.hotfix-stop{position:relative;margin-top:11px;padding:10px 12px 10px 38px;color:#ffd1c4;background:#3a1712;border:1px solid #a93823;
 border-radius:3px;font:11px/1.4 Shentox,""Arial Narrow"",sans-serif;letter-spacing:.35px}
.hotfix-stop:before{content:'!';position:absolute;left:13px;top:8px;width:17px;height:17px;color:#fff2d5;text-align:center;
 font:bold 13px/17px Arial;background:#d84324;border:1px solid #ff8a63;border-radius:50%;animation:stopFlash 1.3s ease-in-out infinite}
.hotfix-foot{display:flex;align-items:center;justify-content:space-between;padding:13px 19px 16px;border-top:1px solid #454849;background:#202223}
.hotfix-foot-note{max-width:235px;color:#929797;font-size:10px;line-height:1.4}.hotfix-buttons{display:flex;align-items:center}
.hotfix-buttons .btn+.btn{margin-left:9px}.hotfix-confirm{min-width:174px;overflow:hidden;color:#fff5dc;border-color:#ff6a3b;
 background:linear-gradient(#f05a31,#b82717 62%,#74160e);box-shadow:inset 0 2px #ff8967,0 3px 0 #4d0e09;text-shadow:0 2px #5a100a}
.hotfix-confirm:hover{border-color:#ffc06b;background:linear-gradient(#ff754d,#d53620 62%,#881b10)}
.hotfix-confirm:before{content:'';position:absolute;top:0;bottom:0;left:-55%;width:42%;
 background:linear-gradient(90deg,transparent,rgba(255,222,130,.45),transparent);transform:skewX(-18deg);animation:dangerSweep 2.1s ease-in-out infinite}
.hotfix-confirm span{display:inline-block;margin-right:7px;width:17px;height:17px;border-radius:50%;color:#65130c;background:#ffd046;
 font:bold 12px/17px Arial;text-shadow:none;vertical-align:middle;animation:buttonDanger 1.25s ease-in-out infinite}
.update-modal{display:none;position:fixed;left:0;right:0;top:0;bottom:0;z-index:210;padding:22px;align-items:center;justify-content:center;
 background:radial-gradient(circle at 50% 36%,rgba(29,115,139,.34),rgba(4,6,7,.95) 67%)}
.update-modal.show{display:flex;animation:hotfixBackdrop .22s ease-out both}
.update-dialog{position:relative;width:680px;max-width:100%;overflow:hidden;background:#222729;border:2px solid #72d3e9;
 border-radius:8px 23px 8px 23px;box-shadow:inset 0 0 0 2px #234d58,0 24px 76px #000;animation:updateDeploy .34s cubic-bezier(.15,.84,.26,1) both}
.update-hazard{position:relative;height:13px;overflow:hidden;border-bottom:2px solid #090a0a;background:#192022}
.update-hazard:before{content:'';position:absolute;left:-72px;top:0;width:calc(100% + 144px);height:13px;
 background:repeating-linear-gradient(135deg,#72d3e9 0,#72d3e9 17px,#1a1d1e 17px,#1a1d1e 34px);
 animation:updateHazard 1.25s steps(24,end) infinite}
.update-head{display:flex;align-items:center;padding:17px 21px 15px;background:linear-gradient(100deg,#18343d,#262a2b 56%,#37301e);border-bottom:1px solid #090a0a}
.update-emblem{width:58px;height:58px;flex:0 0 58px;margin-right:16px}.update-emblem svg{display:block;width:58px;height:58px;overflow:visible}
.update-emblem-shadow{fill:#090a0a;stroke:#090a0a;stroke-width:6;stroke-linejoin:round}.update-emblem-rim{fill:#ffd046;stroke:#151718;stroke-width:3;stroke-linejoin:round}
.update-emblem-face{fill:#20353b;stroke:#83def2;stroke-width:2;stroke-linejoin:round}.update-emblem-arrow{fill:none;stroke:#fff4a5;stroke-width:5;stroke-linecap:square;stroke-linejoin:miter}
.update-heading{min-width:0}.update-heading strong{display:block;color:#f7f8f3;font:16px Shentox,""Arial Black"",sans-serif;letter-spacing:.8px}
.update-heading span{display:block;margin-top:4px;color:#79d4ea;font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1.1px}
.update-body{padding:16px 21px 17px}.update-intro{margin:0 0 13px;color:#d5d8d6;font-size:12px;line-height:1.5}
.update-version-rail{display:flex;align-items:center;padding:13px;background:#171a1b;border:1px solid #4a5558;border-radius:4px 13px 4px 13px;box-shadow:inset 0 2px 5px #090a0a}
.update-version-node{width:176px;flex:0 0 176px}.update-version-node b{display:block;color:#778184;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.9px}
.update-version-node strong{display:block;margin-top:4px;color:#e8e9e5;font:19px Shentox,""Arial Black"",sans-serif;letter-spacing:.5px}
.update-version-node.latest{text-align:right}.update-version-node.latest b{color:#ffd046}.update-version-node.latest strong{color:#fff1a2}
.update-flow{position:relative;flex:1;height:24px;margin:0 15px;overflow:hidden}.update-flow:before{content:'';position:absolute;left:0;right:0;top:11px;height:3px;background:#35484d;border-top:1px solid #6d8c94}
.update-flow:after{content:'';position:absolute;left:4px;top:6px;width:11px;height:11px;border-top:4px solid #70d4ea;border-right:4px solid #70d4ea;
 transform:rotate(45deg);animation:updatePacket 1.35s ease-in-out infinite}
.update-proof{display:flex;margin:11px -4px 0}.update-proof div{flex:1;margin:0 4px;padding:8px 9px;color:#9fa7a7;background:#1a1d1e;border:1px solid #3f4749;
 border-radius:3px;font-size:9px;line-height:1.4}.update-proof b{display:block;margin-bottom:3px;color:#7ed9ec;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.7px}
.update-status{margin-top:11px;padding:9px 11px;color:#a7b1b2;background:#192124;border:1px solid #3f6068;border-radius:3px;font-size:10px;line-height:1.45}
.update-status.bad{color:#ffd0c4;background:#371914;border-color:#9d3e29}.update-status.good{color:#b8eadf;background:#183129;border-color:#3d7c6e}
.update-progress{display:none;height:8px;margin-top:10px;padding:2px;background:#0d1011;border:1px solid #465052;border-radius:5px;overflow:hidden}
.update-progress span{display:block;width:0;height:100%;background:linear-gradient(90deg,#3d8ca0,#78d9ed 72%,#ffd046);
 box-shadow:0 0 8px rgba(120,217,237,.45);transition:width .18s ease-out}
.update-modal.installing .update-progress{display:block}.update-modal.installing .update-flow:after{animation-duration:.65s}
.update-foot{display:flex;align-items:center;justify-content:space-between;padding:13px 21px 16px;background:#1e2223;border-top:1px solid #454c4e}
.update-foot-note{max-width:270px;color:#899597;font-size:9px;line-height:1.45}.update-buttons{display:flex;align-items:center}.update-buttons .btn+.btn{margin-left:8px}
.update-install{min-width:174px;overflow:hidden;color:#2b250d;border-color:#ffe072;background:linear-gradient(#fff08a,#ffd046 58%,#d99a14);
 box-shadow:inset 0 2px #fff9c4,0 3px 0 #76540d;text-shadow:0 1px rgba(255,255,255,.45)}
.update-install:hover{border-color:#fff5af;background:linear-gradient(#fff6ad,#ffda55 58%,#e9a51b)}
.update-install:before{content:'';position:absolute;top:0;bottom:0;left:-50%;width:36%;background:linear-gradient(90deg,transparent,rgba(255,255,255,.68),transparent);
 transform:translate3d(0,0,0) skewX(-18deg);animation:updateButtonSweep 2.4s ease-in-out infinite}
.update-install:disabled{cursor:not-allowed;color:#7e8382;border-color:#52595a;background:#333738;box-shadow:inset 0 1px #525859,0 3px 0 #151718;text-shadow:none}
.update-install:disabled:before{display:none}
.update-toast{position:fixed;right:24px;bottom:24px;z-index:225;width:390px;max-width:calc(100% - 48px);padding:12px 40px 12px 15px;
 color:#c7d0cf;background:#202728;border:1px solid #5790a0;border-left:5px solid #71d3e9;border-radius:4px 13px 4px 13px;
 box-shadow:0 14px 40px #000;opacity:0;visibility:hidden;transform:translate3d(18px,0,0);transition:opacity .2s,transform .25s,visibility .25s}
.update-toast.show{opacity:1;visibility:visible;transform:translate3d(0,0,0)}.update-toast.good{border-left-color:#58d5a8}.update-toast.bad{border-color:#9d422d;border-left-color:#ff6840}
.update-toast b{display:block;color:#f4f5ef;font:10px Shentox,""Arial Black"",sans-serif;letter-spacing:.55px}.update-toast span{display:block;margin-top:4px;font-size:10px;line-height:1.45}
.update-toast button{position:absolute;right:8px;top:8px;width:25px;height:25px;padding:0;color:#aab2b2;background:#181b1c;border:1px solid #4e5759;border-radius:3px;cursor:pointer;font:bold 14px Arial}
.dependency-modal{z-index:170}
.dependency-modal .hotfix-dialog{width:720px;border-color:#e5ad22;box-shadow:inset 0 0 0 2px #58440f,0 22px 70px #000}
.dependency-modal .hotfix-head{background:linear-gradient(90deg,#403515,#292b2c 58%,#18333c)}
.dependency-modal .hotfix-title strong{color:#ffd046}
.dependency-modal .hotfix-foot-note{max-width:210px}.dependency-modal .hotfix-confirm{min-width:330px;font-size:9px}
.developer-command-modal{z-index:175}
.developer-command-modal .hotfix-dialog{width:650px;border-color:#d9a31d;box-shadow:inset 0 0 0 2px #58440f,0 22px 70px #000}
.developer-command-modal .hotfix-head{background:linear-gradient(90deg,#403515,#292b2c 58%,#17313a)}
.developer-command-modal .hotfix-title strong{color:#ffd046}
.developer-command-modal .hotfix-confirm{min-width:225px}
.command-access-grid{display:flex;margin:0 -5px 11px}
.command-access-option{position:relative;flex:1;min-height:102px;margin:0 5px;padding:12px 13px 11px 47px;text-align:left;cursor:pointer;outline:0;
 color:#b7bdbc;background:#1b1e1f;border:2px solid #4b5355;border-radius:4px 13px 4px 13px;box-shadow:inset 0 1px #303536}
.command-access-option:hover{border-color:#7c979e;background:#20282b}.command-access-option.selected{color:#e8e9e5;border-color:#e0aa20;
 background:linear-gradient(110deg,#322b1b,#202729 76%);box-shadow:inset 0 1px #5b4b21,0 0 13px rgba(255,208,70,.12)}
.command-access-bolt{position:absolute;left:13px;top:14px;width:23px;height:23px;transform:rotate(45deg);background:#25292a;border:2px solid #697174;border-radius:4px}
.command-access-bolt:after{content:'';position:absolute;left:7px;top:7px;width:5px;height:5px;background:#92999a;border-radius:50%}
.command-access-option.selected .command-access-bolt{background:#ffd046;border-color:#17191a;box-shadow:0 0 0 2px #9d7415,0 0 9px rgba(255,208,70,.35)}
.command-access-option.selected .command-access-bolt:after{background:#33270b}
.command-access-option strong{display:block;color:#f5f5ef;font:12px Shentox,""Arial Black"",sans-serif;letter-spacing:.55px}
.command-access-option small{display:block;margin-top:3px;color:#75cde4;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.8px}
.command-access-option span:last-child{display:block;margin-top:7px;font-size:10px;line-height:1.42}
.command-everyone-warning{display:none;margin-top:10px}.command-everyone-warning.show{display:block;animation:helpCardIn .22s ease-out both}
.command-access-ack{margin-top:9px;padding:9px 11px;background:#1a1c1d;border:1px solid #8d3928;border-radius:3px}
.command-access-ack label{display:flex;align-items:center;color:#ffd2c5;font-size:10px;line-height:1.4;cursor:pointer}
.command-access-ack input{position:absolute;left:-9999px}
.command-access-box{position:relative;width:20px;height:20px;flex:0 0 20px;margin-right:9px;background:#0e1011;border:2px solid #77625d;border-radius:3px}
.command-access-ack input:checked+.command-access-box{border-color:#ff6a3b;background:#8f2415;box-shadow:0 0 8px rgba(255,82,43,.4)}
.command-access-ack input:checked+.command-access-box:after{content:'\2713';position:absolute;left:3px;top:-1px;color:#fff4d8;font:bold 15px/20px Arial}
.developer-command-modal .hotfix-confirm:disabled{cursor:not-allowed;color:#8d8581;border-color:#5d5552;background:#353535;box-shadow:inset 0 1px #555,0 3px 0 #171717;text-shadow:none;opacity:.65}
.developer-command-modal .hotfix-confirm:disabled:before,.developer-command-modal .hotfix-confirm:disabled span{display:none}
.cannon-danger-modal{z-index:180}
.cannon-danger-modal .hotfix-dialog{width:690px;border-color:#ff4f2c;box-shadow:inset 0 0 0 2px #681d12,0 22px 75px #000,0 0 30px rgba(255,64,30,.18)}
.cannon-danger-modal .hotfix-title strong{color:#ff7654}
.cannon-danger-modal .hotfix-checks{counter-reset:safety-step}
.cannon-danger-modal .hotfix-checks li{counter-increment:safety-step}
.cannon-danger-modal .hotfix-checks li:before{content:counter(safety-step);color:#ffb055;font:10px Shentox,""Arial Black"",sans-serif}
.cannon-danger-ack{margin-top:11px;padding:10px 12px;background:#1b1d1e;border:1px solid #626768;border-radius:3px}
.cannon-danger-ack label{display:flex;align-items:center;color:#e4e6e2;font-size:10px;line-height:1.45;cursor:pointer}
.cannon-danger-ack input{position:absolute;left:-9999px}
.cannon-danger-box{position:relative;width:21px;height:21px;flex:0 0 21px;margin-right:10px;background:#0f1112;border:2px solid #6f7576;border-radius:3px;
 box-shadow:inset 0 2px 5px #000}
.cannon-danger-ack input:checked+.cannon-danger-box{border-color:#ff6a3b;background:#8f2415;box-shadow:inset 0 1px #db5f44,0 0 9px rgba(255,82,43,.42)}
.cannon-danger-ack input:checked+.cannon-danger-box:after{content:'\2713';position:absolute;left:3px;top:-1px;color:#fff4d8;font:bold 16px/21px Arial}
.cannon-danger-modal .hotfix-confirm:disabled{cursor:not-allowed;color:#8d8581;border-color:#5d5552;background:#353535;box-shadow:inset 0 1px #555,0 3px 0 #171717;text-shadow:none;opacity:.65}
.cannon-danger-modal .hotfix-confirm:disabled:before,.cannon-danger-modal .hotfix-confirm:disabled span{display:none}

.onboard-modal,.help-modal{display:none;position:fixed;z-index:130;left:0;right:0;top:0;bottom:0;padding:22px;
 align-items:center;justify-content:center;background:radial-gradient(circle at 50% 35%,rgba(44,109,130,.25),rgba(5,6,7,.94) 68%)}
.onboard-modal.show,.help-modal.show{display:flex;animation:hotfixBackdrop .22s ease-out both}
.onboard-dialog{position:relative;width:590px;max-width:100%;overflow:hidden;background:#24282a;border:2px solid #ffd046;
 border-radius:9px 24px 9px 24px;box-shadow:inset 0 0 0 2px #574613,0 24px 75px #000;
 animation:helpDeploy .38s cubic-bezier(.14,.83,.25,1) both}
.onboard-hazard,.help-hazard{height:12px;border-bottom:2px solid #080909;background-size:51px 51px;
 background-image:repeating-linear-gradient(135deg,#ffd046 0,#ffd046 18px,#1a1c1d 18px,#1a1c1d 36px);animation:hazardMove 1.4s linear infinite}
.onboard-main{display:flex;align-items:center;padding:24px 27px 20px;background:linear-gradient(115deg,#20333a,#252829 52%,#342d1e)}
.onboard-mark{position:relative;width:82px;height:82px;flex:0 0 82px;margin-right:24px}
.onboard-mark:before{content:'';position:absolute;left:17px;top:17px;width:48px;height:48px;box-sizing:border-box;
 transform:rotate(45deg);border:4px solid #ffd046;border-radius:8px;background:#26291f;
 box-shadow:0 0 0 3px #101112,0 0 25px rgba(255,208,70,.35);animation:onboardMark 2s ease-in-out infinite}
.onboard-mark span{position:absolute;left:17px;top:17px;width:48px;height:48px;color:#ffd046;text-align:center;
 font:bold 29px/48px Arial;text-shadow:0 2px #000;animation:onboardQuestion 2s ease-in-out infinite}
.onboard-copy{min-width:0}.onboard-kicker{color:#7cd7ee;font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1.4px}
.onboard-copy h2{margin:5px 0 8px;color:#fff;font:21px/1.1 Shentox,""Arial Black"",sans-serif;letter-spacing:.8px}
.onboard-copy p{margin:0;color:#c3c8c7;font-size:12px;line-height:1.58}
.onboard-preview{display:flex;padding:14px 19px;background:#191b1c;border-top:1px solid #45494a;border-bottom:1px solid #090a0a}
.onboard-preview div{flex:1;position:relative;padding:6px 9px 6px 29px;color:#aeb4b3;font-size:10px;line-height:1.35}
.onboard-preview div:before{content:'✓';position:absolute;left:7px;top:5px;color:#ffd046;font:bold 13px Arial}
.onboard-actions{display:flex;align-items:center;justify-content:space-between;padding:14px 19px 17px;background:#222526}
.onboard-actions p{max-width:265px;margin:0;color:#858b8b;font-size:10px;line-height:1.45}.onboard-actions .btn+.btn{margin-left:8px}

.help-modal{z-index:135}.help-dialog{position:relative;width:790px;height:680px;max-width:100%;max-height:calc(100% - 6px);display:flex;flex-direction:column;
 overflow:hidden;background:#222628;border:2px solid #5e9aae;border-radius:8px 23px 8px 23px;
 box-shadow:inset 0 0 0 2px #183c48,0 25px 80px #000;animation:helpDeploy .34s cubic-bezier(.14,.83,.25,1) both}
.help-hazard{flex:0 0 12px;background-image:repeating-linear-gradient(135deg,#58b9d5 0,#58b9d5 18px,#17282e 18px,#17282e 36px)}
.help-head{display:flex;align-items:center;padding:15px 19px;background:linear-gradient(90deg,#173642,#25292a 58%);border-bottom:1px solid #080909}
.help-emblem{width:39px;height:39px;flex:0 0 39px;margin-right:14px;color:#17282e;background:#73d2ec;border:3px solid #172024;
 border-radius:50%;font:bold 23px/33px Arial;text-align:center;box-shadow:0 0 0 2px #4b91a5,0 0 16px rgba(82,190,222,.3)}
.help-heading{min-width:0;flex:1}.help-heading strong{display:block;color:#fff;font:16px Shentox,""Arial Black"",sans-serif;letter-spacing:.75px}
.help-heading span{display:block;margin-top:4px;color:#8fc6d5;font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.8px}
.help-close{width:35px;height:35px;padding:0;color:#abb2b2;background:#242829;border:1px solid #596061;border-radius:8px;cursor:pointer;font:bold 18px Arial}
.help-close:hover{color:#fff;background:#a93624;border-color:#ff7654}
.help-body{flex:1;min-height:0;overflow-y:auto;padding:15px 17px 18px;background:#191c1d;
 scrollbar-face-color:#437789;scrollbar-track-color:#111415;scrollbar-arrow-color:#9de8fb;
 scrollbar-highlight-color:#6fabbc;scrollbar-shadow-color:#15282e;scrollbar-3dlight-color:#20282a;scrollbar-darkshadow-color:#090b0c}
.help-quick{display:flex;margin:0 -4px 11px}.help-step{flex:1;margin:4px;padding:10px 9px;background:#203039;border:1px solid #386a79;
 border-radius:3px 11px 3px 11px;animation:helpCardIn .35s ease-out both}
.help-step:nth-child(2){animation-delay:.04s}.help-step:nth-child(3){animation-delay:.08s}.help-step:nth-child(4){animation-delay:.12s}
.help-step b{display:block;color:#7cd7ee;font:15px Shentox,""Arial Black"",sans-serif}.help-step span{display:block;margin-top:4px;color:#c2c7c6;font-size:10px;line-height:1.4}
.help-section{margin-top:11px;border:1px solid #414849;background:#222526;border-radius:4px 13px 4px 13px;overflow:hidden;
 animation:helpCardIn .35s ease-out both}
.help-section-title{padding:8px 11px;color:#ffd046;background:#292d2e;border-left:4px solid #ffd046;
 font:11px Shentox,""Arial Black"",sans-serif;letter-spacing:.7px}
.help-grid{display:flex;flex-wrap:wrap;padding:5px}.help-item{width:50%;padding:6px 8px 7px}
.help-item b{display:block;margin-bottom:3px;color:#f3f4ef;font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.35px}
.help-item p{margin:0;color:#aeb4b3;font-size:10px;line-height:1.5}.help-item p strong{color:#ffd046}
.help-danger{margin:10px 8px 8px;padding:9px 11px;color:#ffd0c2;background:#391813;border:1px solid #9f3725;border-radius:4px;
 font-size:10px;line-height:1.5}.help-danger b{color:#ff7654;font-family:Shentox,""Arial Black"",sans-serif}
.help-foot{flex:0 0 auto;display:flex;align-items:center;justify-content:space-between;padding:12px 17px 15px;background:#25292a;border-top:1px solid #4a4e4f}
.help-status{max-width:255px;color:#8da4aa;font-size:10px;line-height:1.4}.help-status.good{color:#8bd8c6}.help-buttons{display:flex}.help-buttons .btn+.btn{margin-left:8px}

.secret-mods-layer{display:none;position:fixed;z-index:99;left:0;right:0;top:38px;bottom:0;
 background:radial-gradient(circle at 126px 0,rgba(255,208,70,.17),transparent 310px),rgba(4,6,7,.72)}
.secret-mods-layer.show{display:block;animation:secretBackdropWake .22s ease-out both}
.secret-mods-panel{position:absolute;left:12px;top:9px;width:650px;height:calc(100% - 18px);max-width:calc(100% - 24px);max-height:690px;overflow:hidden;
 display:flex;flex-direction:column;
 color:#f4f4ef;background:#222628;border:2px solid #e5ad22;border-radius:5px 18px 5px 18px;
 box-shadow:inset 0 0 0 2px #58440f,0 18px 55px rgba(0,0,0,.82),0 0 24px rgba(255,193,35,.18);
 transform-origin:26px -19px;animation:secretPanelDeploy .44s cubic-bezier(.13,.86,.27,1) both}
.secret-mods-panel:before,.secret-mods-panel:after{content:'';position:absolute;z-index:4;width:7px;height:7px;border-radius:50%;
 background:#777b7c;border:2px solid #101112;box-shadow:inset 0 1px #c5c7c7}
.secret-mods-panel:before{left:7px;top:19px}.secret-mods-panel:after{right:7px;bottom:7px}
.secret-mods-hazard{position:relative;height:11px;flex:0 0 11px;overflow:hidden;border-bottom:2px solid #080909;background:#1a1c1d}
.secret-mods-hazard:before{content:'';position:absolute;left:-72px;top:0;width:calc(100% + 144px);height:11px;
 background-size:42px 42px;background-image:repeating-linear-gradient(135deg,#ffd046 0,#ffd046 15px,#1a1c1d 15px,#1a1c1d 30px);
 animation:secretHazardFlow 1.2s steps(24,end) infinite}
.secret-mods-head{display:flex;flex:0 0 auto;align-items:center;padding:13px 18px 12px;background:linear-gradient(100deg,#2d2a1d,#242829 58%,#18333c);
 border-bottom:1px solid #080909}
.secret-mods-mark{position:relative;width:46px;height:46px;flex:0 0 46px;margin-right:14px}
.secret-mods-mark svg{display:block;width:46px;height:46px;overflow:visible;transform-origin:50% 50%;
 animation:secretMarkSignal 2.2s ease-in-out infinite}
.secret-mods-mark-shadow{fill:#090a0a;stroke:#090a0a;stroke-width:6;stroke-linejoin:round}
.secret-mods-mark-rim{fill:#d99f17;stroke:#121415;stroke-width:3;stroke-linejoin:round}
.secret-mods-mark-face{fill:#ffd046;stroke:#fff09a;stroke-width:1.5;stroke-linejoin:round}
.secret-mods-letter-highlight{fill:#fff3a2;opacity:.65}
.secret-mods-letter-face{fill:#252719}
.secret-mods-heading{flex:1;min-width:0}.secret-mods-heading strong{display:block;color:#fff;
 font:17px Shentox,""Arial Black"",sans-serif;letter-spacing:.8px}
.secret-mods-heading span{display:block;margin-top:3px;color:#79cfe6;font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1.25px}
.secret-mods-close{width:34px;height:34px;padding:0;color:#9fa5a5;background:#202324;border:1px solid #575c5d;
 border-radius:4px 10px 4px 10px;cursor:pointer;font:bold 17px Arial}
.secret-mods-close:hover{color:#fff;background:#a93624;border-color:#ff7654}
.secret-mods-body{display:flex;flex:1;min-height:0;flex-direction:column;padding:11px 14px 13px;background:linear-gradient(180deg,#1b1e1f,#17191a)}
.secret-mods-warning{position:relative;flex:0 0 auto;margin-bottom:8px;padding:7px 10px 7px 34px;color:#d5bf77;background:#302b1e;
 border:1px solid #75601e;border-radius:3px;font-size:10px;line-height:1.45}
.secret-mods-warning:before{content:'!';position:absolute;left:10px;top:7px;width:15px;height:15px;color:#28220e;
 background:#ffd046;border-radius:50%;font:bold 11px/15px Arial;text-align:center}
.secret-mod-row{display:flex;align-items:center;min-height:66px;padding:10px 11px;background:#22282a;border:1px solid #4a5558;
 border-radius:4px 13px 4px 13px;box-shadow:inset 0 1px #333a3c;transition:border-color .2s,background .2s}
.secret-mod-row+.secret-mod-row{margin-top:7px}
.secret-mod-row.enabled{border-color:#b88a1c;background:linear-gradient(90deg,#302b1d,#22282a 70%)}
.secret-mod-row.locked{opacity:.48}
.secret-mod-copy{flex:1;min-width:0;padding-right:13px}.secret-mod-copy strong{display:block;color:#f4f4ef;
 font:12px Shentox,""Arial Black"",sans-serif;letter-spacing:.55px}
.secret-mod-copy span{display:block;margin-top:5px;color:#9fa6a6;font-size:10px;line-height:1.42}
.secret-mod-copy em{display:inline-block;margin-top:7px;padding:3px 6px;color:#8f999b;background:#171a1b;border:1px solid #43494a;
 border-radius:3px;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.75px;font-style:normal}
.secret-mod-row.enabled .secret-mod-copy em{color:#ffe17a;border-color:#96701a;background:#292417}
.secret-compat-reason{display:none!important;margin-top:5px!important;color:#e4a28f!important;font-size:9px!important;line-height:1.38!important}
.secret-compat-reason.show{display:block!important}
.secret-mod-actions{display:flex;box-sizing:border-box;width:68px;min-width:68px;max-width:68px;flex:0 0 68px;flex-direction:column;align-items:stretch}
.secret-mod-actions .secret-switch{width:68px;min-width:68px;max-width:68px;flex:0 0 34px;align-self:center}
.secret-mod-options{box-sizing:border-box;width:68px;min-width:68px;max-width:68px;height:23px;margin-bottom:6px;padding:0;color:#84cfe2;background:#182125;border:1px solid #47636a;border-radius:3px 8px 3px 8px;
 cursor:pointer;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.55px}
.secret-mod-options:hover{color:#e8f9fd;background:#24404a;border-color:#73cde5}.secret-mod-options:disabled{cursor:not-allowed;opacity:.45}
.secret-master-row{flex:0 0 auto;min-height:58px}
.secret-mods-catalog-head{display:flex;flex:0 0 auto;align-items:flex-end;justify-content:space-between;margin-top:9px;padding:0 2px 7px;border-bottom:1px solid #3b4446}
.secret-mods-catalog-label b{display:block;color:#73cee6;font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.9px}
.secret-mods-catalog-label span{display:block;margin-top:3px;color:#858f91;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.65px}
.secret-mod-search{position:relative;width:245px;height:31px;background:#111415;border:1px solid #4c5b5f;border-radius:3px 9px 3px 9px;box-shadow:inset 0 2px 5px #050606}
.secret-mod-search:before{content:'';position:absolute;left:10px;top:8px;width:9px;height:9px;border:2px solid #76cde4;border-radius:50%}
.secret-mod-search:after{content:'';position:absolute;left:20px;top:18px;width:6px;height:2px;background:#76cde4;transform:rotate(45deg)}
.secret-mod-search input{position:absolute;left:32px;right:8px;top:1px;width:calc(100% - 40px);height:27px;padding:0;color:#eef3f2;background:transparent;border:0;outline:0;
 font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.55px}
.secret-mod-search input::placeholder{color:#657174}
.secret-mods-list{position:relative;flex:1;min-height:125px;margin-top:7px;padding:1px 8px 3px 1px;overflow-x:hidden;overflow-y:auto;
 scrollbar-face-color:#45575b;scrollbar-track-color:#101314;scrollbar-arrow-color:#8fddf0;scrollbar-shadow-color:#0b0d0e;scrollbar-highlight-color:#657b80}
.secret-mods-list::-webkit-scrollbar{width:11px}.secret-mods-list::-webkit-scrollbar-track{background:#101314;border:1px solid #050606}
.secret-mods-list::-webkit-scrollbar-thumb{background:linear-gradient(90deg,#33454a,#587078);border:2px solid #101314;border-radius:5px}
.secret-mods-list::-webkit-scrollbar-thumb:hover{background:linear-gradient(90deg,#45616a,#70a5b4)}
.secret-mod-card{animation:secretCardDock .26s ease-out both}.secret-mod-card:nth-child(2){animation-delay:.035s}.secret-mod-card:nth-child(3){animation-delay:.07s}.secret-mod-card:nth-child(4){animation-delay:.105s}.secret-mod-card:nth-child(5){animation-delay:.14s}
.secret-mod-tag{display:inline-block;margin:0 0 5px!important;color:#6fcbe3!important;font:8px Shentox,""Arial Narrow"",sans-serif!important;letter-spacing:.8px!important}
.secret-mods-empty{display:none;margin:18px 5px;padding:18px;text-align:center;color:#758184;background:#15191a;border:1px dashed #415054;border-radius:4px;
 font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.7px}
.secret-switch{position:relative;box-sizing:border-box;width:68px;min-width:68px;max-width:68px;height:34px;min-height:34px;max-height:34px;flex:0 0 68px;padding:0;cursor:pointer;outline:none;
 background:#151718;border:2px solid #565b5c;border-radius:7px;box-shadow:inset 0 3px 7px #060707,0 2px #080909}
.secret-switch:disabled{cursor:not-allowed;filter:alpha(opacity=55);opacity:.55}
.secret-switch-track{position:absolute;left:7px;right:7px;top:13px;height:5px;background:#4b5051;border-radius:4px;
 box-shadow:inset 0 1px #191b1c}
.secret-switch-knob{position:absolute;left:4px;top:4px;width:22px;height:22px;background:linear-gradient(#8a8f8f,#555a5b);
 border:2px solid #1a1c1d;border-radius:5px;box-shadow:inset 0 1px #bfc2c2,0 1px 3px #000;
 transition:left .24s cubic-bezier(.18,.82,.28,1),background .2s,box-shadow .2s}
.secret-switch.on{border-color:#c08e18}.secret-switch.on .secret-switch-track{background:#d69c15;box-shadow:0 0 8px rgba(255,208,70,.45)}
.secret-switch.on .secret-switch-knob{left:38px;background:linear-gradient(#fff29a,#ffd046 55%,#d68c0d);
 box-shadow:inset 0 1px #fff9cb,0 0 10px rgba(255,208,70,.6)}
.secret-mods-slots{flex:0 0 auto;margin-top:8px;padding:7px 10px;border:1px dashed #45575d;background:#192124;border-radius:3px;
 color:#78919a;font-size:9px;line-height:1.45;letter-spacing:.35px;white-space:nowrap}
.secret-mods-slots b{display:inline;margin-right:7px;color:#72cce5;font:10px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.8px}
.secret-mods-feedback{display:none;flex:0 0 auto;margin-top:8px;padding:7px 10px;border:1px solid #566062;background:#202426;border-radius:3px;
 color:#aeb5b5;font-size:9px;line-height:1.45}
.secret-mods-feedback.show{display:block}.secret-mods-feedback.good{display:block;color:#a9dfd3;border-color:#3f8273;background:#1b302c}
.secret-mods-feedback.bad{display:block;color:#ffad96;border-color:#9e3e29;background:#361a15}
.secret-mods-feedback.working{display:block;color:#ffe17b;border-color:#96701a;background:#302817;animation:warningBlink 1.2s ease-in-out infinite}
.secret-mods-status{display:flex;flex:0 0 auto;align-items:center;margin-top:8px;padding-top:8px;border-top:1px solid #383d3e;
 color:#838a8a;font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.75px}
.secret-mods-status i{width:7px;height:7px;margin-right:7px;background:#656b6b;border-radius:50%;box-shadow:0 0 0 2px #151718}
.secret-mods-status.on{color:#e2c15f}.secret-mods-status.on i{background:#ffd046;box-shadow:0 0 8px #ffd046}

.tutorial{display:none;position:fixed;z-index:160;left:0;right:0;top:0;bottom:0;overflow:hidden}
.tutorial.show{display:block}.tutorial-shade{position:fixed;z-index:0;background:rgba(4,6,7,.82);pointer-events:none;
 transition:left .38s cubic-bezier(.2,.8,.2,1),top .38s cubic-bezier(.2,.8,.2,1),width .38s cubic-bezier(.2,.8,.2,1),height .38s cubic-bezier(.2,.8,.2,1)}
.tutorial-focus{position:fixed;z-index:1;border:3px solid #ffd046;border-radius:9px;
 box-sizing:border-box;box-shadow:0 0 0 2px #161819,0 0 27px rgba(255,208,70,.75);
 transition:left .38s cubic-bezier(.2,.8,.2,1),top .38s cubic-bezier(.2,.8,.2,1),width .38s cubic-bezier(.2,.8,.2,1),height .38s cubic-bezier(.2,.8,.2,1);
 pointer-events:none}
.tutorial-focus:before{content:'';position:absolute;left:5px;right:5px;top:-7px;height:4px;background:#ffd046;box-shadow:0 0 9px #ffd046;
 transform-origin:50% 50%;animation:tutorialFocusSignal 1.7s ease-in-out infinite}
.tutorial-card{position:fixed;z-index:3;width:430px;max-width:calc(100% - 28px);overflow:hidden;background:#25292a;border:2px solid #65bfd8;
 border-radius:6px 18px 6px 18px;box-shadow:inset 0 0 0 2px #193c47,0 18px 55px #000;pointer-events:auto}
.tutorial-card.enter{animation:tutorialCardIn .4s cubic-bezier(.16,.84,.28,1) both}
.tutorial-rail{position:relative;height:10px;overflow:hidden;background:#17282e;border-bottom:1px solid #080909;
 box-shadow:inset 0 1px rgba(186,241,255,.35),inset 0 -1px #0d171a}
.tutorial-rail:before{content:'';position:absolute;left:-84px;top:0;width:calc(100% + 168px);height:10px;
 background-size:42px 42px;background-image:repeating-linear-gradient(135deg,#63c6e2 0,#63c6e2 15px,#17282e 15px,#17282e 30px);
 transform:translate3d(-42px,0,0);backface-visibility:hidden;animation:tutorialRailFlow 1.25s steps(24,end) infinite;pointer-events:none}
.tutorial-rail:after{content:'';position:absolute;left:0;right:0;top:0;height:2px;background:rgba(194,243,255,.42);pointer-events:none}
.tutorial-content{padding:15px 17px 12px}.tutorial-meta{display:flex;align-items:center;margin-bottom:8px}
.tutorial-number{position:relative;width:50px;height:50px;min-width:50px;max-width:50px;flex:0 0 50px;margin-right:12px}
.tutorial-number svg{position:absolute;left:0;top:0;width:50px;height:50px;display:block;overflow:visible}
.tutorial-number-shadow{fill:#070808;stroke:#070808;stroke-width:6;stroke-linejoin:round}
.tutorial-number-mount{fill:#202324;stroke:#090a0a;stroke-width:3;stroke-linejoin:round}
.tutorial-number-rim{fill:#d49312;stroke:#ffd046;stroke-width:2.4;stroke-linejoin:round}
.tutorial-number-face{fill:url(#tutorialBadgeFaceGradient);stroke:#8b5b0c;stroke-width:1.5;stroke-linejoin:round}
.tutorial-number-inset{fill:none;stroke:#f5ba24;stroke-width:1;stroke-linejoin:round;opacity:.8}
.tutorial-number-highlight{fill:none;stroke:#fff2a0;stroke-width:1.6;stroke-linecap:square;stroke-linejoin:miter;opacity:.82}
.tutorial-number-shade{fill:none;stroke:#a86909;stroke-width:2;stroke-linecap:square;stroke-linejoin:miter;opacity:.85}
.tutorial-number-text{fill:#202318;stroke:#fff0a0;stroke-width:.35;font:bold 16px Arial;text-anchor:middle}
.tutorial-label{color:#75cde5;font:9px Shentox,""Arial Narrow"",sans-serif;letter-spacing:1.2px}.tutorial-title{margin-top:3px;color:#fff;
 font:16px Shentox,""Arial Black"",sans-serif;letter-spacing:.4px}
.tutorial-text{margin:0;color:#c5cac9;font-size:11px;line-height:1.58}.tutorial-tip{position:relative;margin-top:10px;padding:8px 9px 8px 31px;
 color:#ecd07c;background:#332d1e;border:1px solid #72571d;border-radius:3px;font-size:10px;line-height:1.45}
.tutorial-tip:before{content:'i';position:absolute;left:10px;top:8px;width:14px;height:14px;color:#302716;background:#ffd046;border-radius:50%;
 font:bold 10px/14px Arial;text-align:center}
.tutorial-progress{display:flex;margin:0 17px 12px}.tutorial-progress i{height:5px;flex:1;margin-right:4px;background:#414748;border-radius:3px}
.tutorial-progress i:last-child{margin-right:0}.tutorial-progress i.done{background:#4f9bb0}.tutorial-progress i.current{background:#ffd046;box-shadow:0 0 7px rgba(255,208,70,.55)}
.tutorial-actions{display:flex;align-items:center;justify-content:space-between;padding:11px 14px 14px;border-top:1px solid #454a4b;background:#202324}
.tutorial-actions-left{display:flex}.tutorial-actions .btn+.btn{margin-left:7px}.tutorial-next{min-width:112px}

.busy{display:none;position:fixed;top:0;right:0;bottom:0;left:0;z-index:20;background:rgba(8,9,9,.88);align-items:center;justify-content:center}
.busy.show{display:flex}.busy-card{position:relative;width:330px;padding:22px 25px 20px;background:#292c2e;border:2px solid #ffd046;
 border-radius:8px 19px 8px 19px;box-shadow:inset 0 0 0 2px #554515,0 16px 50px #000;text-align:center}
.busy-icon{width:29px;height:29px;margin:0 auto 17px;transform:rotate(45deg);background:#ffd046;border:3px solid #1c1d1e;border-radius:4px}
.busy-icon span{display:block;transform:rotate(-45deg);font:bold 15px/23px Arial;color:#272719}
.busy-card strong{font:13px Shentox,""Arial Black"",sans-serif;letter-spacing:.5px}.busy-card p{margin:6px 0 13px;color:#aeb2b2;font-size:11px}
.loading-status{display:flex;align-items:center;justify-content:space-between;margin:0 1px 5px;color:#737b7c;font:8px Shentox,""Arial Narrow"",sans-serif;letter-spacing:.7px}
.loading-status b{color:#ffd046;font:9px Shentox,""Arial Black"",sans-serif}.loading-track{height:10px;padding:2px;background:#111314;border:1px solid #050606;border-radius:4px;overflow:hidden}
.loading-fill{position:relative;height:4px;width:0;background:linear-gradient(90deg,#b66a0d,#f19d19 55%,#fff17d);
 box-shadow:0 0 7px rgba(255,208,70,.38);transition:width .16s ease-out}.loading-fill:after{content:'';position:absolute;right:0;top:-1px;width:4px;height:6px;background:#fff6a8;box-shadow:0 0 7px #ffd046}
@keyframes hazardMove{0%{background-position:0 0}100%{background-position:51px 0}}
@keyframes mainHazardFlow{0%{transform:translate3d(-51px,0,0)}100%{transform:translate3d(0,0,0)}}
@keyframes panelAssemble{0%{opacity:0;transform:translateY(-8px) scaleY(.96)}100%{opacity:1;transform:translateY(0) scaleY(1)}}
@keyframes indicatorPulse{0%,72%,100%{background:#ffd046;box-shadow:2px 0 #9c6a00}82%{background:#fff3a0;box-shadow:2px 0 #9c6a00,0 0 9px #ffd046}}
@keyframes shutterOpen{0%{opacity:0;transform:scaleY(.2)}100%{opacity:1;transform:scaleY(1)}}
@keyframes buttonSweep{0%{left:-45%}100%{left:125%}}
@keyframes bannerDrop{0%{opacity:0;transform:translateY(-5px)}100%{opacity:1;transform:translateY(0)}}
@keyframes dataBoot{0%{opacity:0;transform:translateY(7px)}100%{opacity:1;transform:translateY(0)}}
@keyframes perfCardIn{0%{opacity:0;transform:translateX(-9px)}100%{opacity:1;transform:translateX(0)}}
@keyframes raidDock{0%{opacity:0;transform:translateX(-12px)}100%{opacity:1;transform:translateX(0)}}
@keyframes meterCharge{0%{opacity:.1;transform:scaleX(.15)}100%{opacity:1;transform:scaleX(1)}}
@keyframes stateBlink{0%,78%,100%{border-color:#5c6061;color:#d6d7d4}88%{border-color:#ffd046;color:#fff5bd}}
@keyframes warningBlink{0%,70%,100%{opacity:1;text-shadow:none}82%{opacity:.45;text-shadow:0 0 7px #ffd046}}
@keyframes hotfixBackdrop{0%{opacity:0}100%{opacity:1}}
@keyframes hotfixDeploy{0%{opacity:0;transform:translateY(-17px) scale(.96)}65%{transform:translateY(2px) scale(1.005)}100%{opacity:1;transform:translateY(0) scale(1)}}
@keyframes dangerPulse{0%,100%{transform:rotate(45deg) scale(1);border-color:#ff5c35}50%{transform:rotate(45deg) scale(1.08);border-color:#ffd046;box-shadow:0 0 0 2px #170907,0 0 22px rgba(255,73,39,.65)}}
@keyframes stopFlash{0%,68%,100%{background:#d84324;box-shadow:none}82%{background:#ff6b3f;box-shadow:0 0 12px rgba(255,82,42,.75)}}
@keyframes dangerSweep{0%,52%{left:-55%}78%,100%{left:125%}}
@keyframes buttonDanger{0%,70%,100%{transform:scale(1)}82%{transform:scale(1.14);box-shadow:0 0 9px #ffd046}}
@keyframes updateDeploy{0%{opacity:0;transform:translate3d(0,-18px,0) scale(.95)}65%{transform:translate3d(0,2px,0) scale(1.004)}100%{opacity:1;transform:translate3d(0,0,0) scale(1)}}
@keyframes updateHazard{0%{transform:translate3d(-34px,0,0)}100%{transform:translate3d(0,0,0)}}
@keyframes updatePacket{0%{opacity:0;transform:translate3d(0,0,0) rotate(45deg)}18%{opacity:1}78%{opacity:1}100%{opacity:0;transform:translate3d(210px,0,0) rotate(45deg)}}
@keyframes updateScan{0%{transform:translate3d(-115%,0,0)}100%{transform:translate3d(300%,0,0)}}
@keyframes updateButtonSweep{0%,58%{transform:translate3d(0,0,0) skewX(-18deg)}82%,100%{transform:translate3d(600%,0,0) skewX(-18deg)}}
@keyframes helpDeploy{0%{opacity:0;transform:translateY(-18px) scale(.96)}68%{transform:translateY(2px) scale(1.004)}100%{opacity:1;transform:translateY(0) scale(1)}}
@keyframes helpCardIn{0%{opacity:0;transform:translateY(7px)}100%{opacity:1;transform:translateY(0)}}
@keyframes onboardMark{0%,100%{transform:rotate(45deg) scale(1)}50%{transform:rotate(45deg) scale(1.08);box-shadow:0 0 0 3px #101112,0 0 34px rgba(255,208,70,.6)}}
@keyframes onboardQuestion{0%,70%,100%{color:#ffd046;transform:scale(1)}82%{color:#fff3a0;transform:scale(1.1)}}
@keyframes tutorialFocusSignal{0%,100%{opacity:.7;transform:scaleX(.68)}50%{opacity:1;transform:scaleX(1)}}
@keyframes tutorialCardIn{0%{opacity:0;transform:translate3d(-18px,12px,0) scale(.94)}68%{opacity:1;transform:translate3d(3px,-1px,0) scale(1.012)}100%{opacity:1;transform:translate3d(0,0,0) scale(1)}}
@keyframes tutorialRailFlow{0%{transform:translate3d(-42px,0,0)}100%{transform:translate3d(0,0,0)}}
@keyframes secretBackdropWake{0%{opacity:0}100%{opacity:1}}
@keyframes secretPanelDeploy{0%{opacity:0;transform:translate3d(-28px,-24px,0) scale(.78) rotate(-2deg)}58%{opacity:1;transform:translate3d(4px,3px,0) scale(1.015) rotate(.25deg)}78%{transform:translate3d(-2px,0,0) scale(.996)}100%{opacity:1;transform:translate3d(0,0,0) scale(1) rotate(0)}}
@keyframes secretHazardFlow{0%{transform:translate3d(-42px,0,0)}100%{transform:translate3d(0,0,0)}}
@keyframes secretMarkSignal{0%,72%,100%{transform:scale(1);opacity:1}82%{transform:scale(1.07);opacity:.88}}
@keyframes secretEmblemUnlock{0%{transform:rotate(45deg) scale(1)}45%{transform:rotate(135deg) scale(1.12);box-shadow:0 0 0 1px #ffd13b,0 0 15px #ffd046}100%{transform:rotate(45deg) scale(1)}}
@keyframes secretCardDock{0%{opacity:0;transform:translate3d(-10px,0,0)}100%{opacity:1;transform:translate3d(0,0,0)}}
@keyframes dropDock{0%{opacity:0;transform:translate3d(0,9px,0) scale(.985)}100%{opacity:1;transform:translate3d(0,0,0) scale(1)}}

@media(max-width:900px){
 .shell{padding-left:13px;padding-right:13px}.local{display:none}.picker{flex-wrap:wrap}.save-picker{width:100%;flex-basis:100%;margin-bottom:8px}
 .picker .save-picker+*{margin-left:0}.picker .btn+.btn{margin-left:8px}.stats{flex-wrap:wrap}.stat{flex-basis:30%}
 .mini{width:33.333%}.raid-meter-wrap{display:none}.repair-bar{align-items:flex-end}.repair-bar p{padding-right:15px}.repair-actions{flex-direction:column}.repair-actions .btn+.btn{margin:8px 0 0}
 .drop-wrap{width:100%}.drop-zone-head{align-items:flex-end}.drop-zone-summary{flex-wrap:wrap;justify-content:flex-end}.drop-count{margin-bottom:5px}
 .drop-scan-panel{padding-right:20px}.drop-scan-panel .btn{position:relative;right:auto;top:auto;margin-top:13px}.item-summary-stats{flex-wrap:wrap}.item-summary-stat{flex-basis:42%;margin-bottom:6px}
 .help-grid{display:block}.help-item{width:100%}.help-quick{flex-wrap:wrap}.help-step{flex-basis:44%}
}
@media(prefers-reduced-motion:reduce){*{animation:none!important;transition:none!important}}
</style>
</head>
<body onload=""boot()"">
<div class=""window-bar"">
  <div class=""window-grip"" onmousedown=""beginWindowDrag()"">
    <div class=""window-emblem secret-trigger"" id=""secretModsTrigger"" aria-label=""Open ScrapLab workshop"" onselectstart=""return false"" onmousedown=""return secretTriggerMouseDown(event)"" onclick=""toggleSecretMods(event)""><div class=""window-emblem-mark"">
      <svg class=""logo-letter"" viewBox=""0 0 22 22"" aria-hidden=""true"">
        <circle class=""logo-world-core"" cx=""11"" cy=""11"" r=""7.2""></circle>
        <path class=""logo-world-line"" d=""M4.6 11h12.8M11 3.8v14.4M6.5 6.7c2.6 1.4 6.4 1.4 9 0M6.5 15.3c2.6-1.4 6.4-1.4 9 0M8.4 4.4c-2.2 3.7-2.2 9.5 0 13.2M13.6 4.4c2.2 3.7 2.2 9.5 0 13.2""></path>
        <path class=""logo-world-glint"" d=""M6.5 6.1c1.1-1.1 2.4-1.7 4-1.9""></path>
      </svg>
    </div></div>
    <div class=""window-title"">SCRAP LAB <span>SURVIVAL WORLD TOOLKIT</span></div>
  </div>
  <div class=""window-controls"">
    <button type=""button"" class=""window-button help"" id=""helpBtn"" title=""Help and tutorial"" aria-label=""Help and tutorial"" onclick=""openHelp()"">
      <svg class=""window-help-icon"" viewBox=""0 0 24 24"" aria-hidden=""true"" focusable=""false"">
        <circle class=""window-help-shadow"" cx=""12"" cy=""12"" r=""10""></circle>
        <circle class=""window-help-ring"" cx=""12"" cy=""12"" r=""9""></circle>
        <path class=""window-help-stem"" d=""M8.8 8.7C9 6.6 10.3 5.5 12.3 5.5C14.4 5.5 15.8 6.7 15.8 8.5C15.8 10 15 10.8 13.7 11.6C12.5 12.3 12 13.1 12 14.3""></path>
        <circle class=""window-help-dot"" cx=""12"" cy=""17.5"" r=""1.05""></circle>
      </svg>
    </button>
    <button type=""button"" class=""window-button minimize"" title=""Minimize"" aria-label=""Minimize"" onclick=""minimizeWindow()""></button>
    <button type=""button"" class=""window-button close"" title=""Close"" aria-label=""Close"" onclick=""closeWindow()""></button>
  </div>
</div>
<div class=""app-scroll"" id=""appScroll"">
<div class=""hazard"" id=""mainHazard""></div>
<div class=""shell"">
  <div class=""topbar"">
    <div class=""identity"" id=""identityPanel"">
      <div class=""brand-mark"">
        <svg class=""logo-letter"" viewBox=""0 0 22 22"" aria-hidden=""true"">
          <circle class=""logo-world-core"" cx=""11"" cy=""11"" r=""7.2""></circle>
          <path class=""logo-world-line"" d=""M4.6 11h12.8M11 3.8v14.4M6.5 6.7c2.6 1.4 6.4 1.4 9 0M6.5 15.3c2.6-1.4 6.4-1.4 9 0M8.4 4.4c-2.2 3.7-2.2 9.5 0 13.2M13.6 4.4c2.2 3.7 2.2 9.5 0 13.2""></path>
          <path class=""logo-world-glint"" d=""M6.5 6.1c1.1-1.1 2.4-1.7 4-1.9""></path>
        </svg>
      </div>
      <div><h1>SCRAP LAB</h1><p>INSPECT &middot; REPAIR &middot; TUNE</p></div>
    </div>
    <div class=""local""><b></b>OFFLINE / LOCAL SAVE ACCESS</div>
  </div>

  <div class=""panel selector-panel"" id=""selectorPanel"">
    <div class=""panel-title""><strong>WORLD SELECTOR</strong><span>SCRAP MECHANIC &middot; CHAPTER 2</span></div>
    <div class=""selector-body"">
      <div id=""gameBanner""></div>
      <div class=""picker"">
        <div class=""save-picker"" id=""savePicker"">
          <button type=""button"" class=""save-display"" id=""saveDisplay"" onclick=""toggleSaveMenu(event)"">
            <span class=""save-name"" id=""saveName"">SCANNING SURVIVAL SAVES...</span>
            <span class=""save-meta"" id=""saveMeta"">Checking the Scrap Mechanic user folder</span>
            <span class=""save-cog""></span>
          </button>
          <div class=""save-menu"" id=""saveMenu""></div>
        </div>
        <button class=""btn"" id=""browseBtn"" onclick=""browseSave()"">BROWSE</button>
        <button class=""btn btn-primary"" id=""analyzeBtn"" onclick=""analyzeSelected()"">ANALYZE WORLD</button>
      </div>
      <div class=""path-row""><span class=""path-label"">SAVE PATH</span><span class=""path"" id=""pathText"">Searching the Scrap Mechanic user directory...</span></div>
    </div>
  </div>

  <div class=""panel diagnostics"" id=""diagnosticsPanel"">
    <div class=""panel-title""><strong>WORLD DIAGNOSTICS</strong><span>PERFORMANCE &middot; RAID RECOVERY &middot; LOOSE ITEMS</span></div>
    <div class=""diagnostic-body"" id=""result"">
      <div class=""empty""><div class=""diamond""><span>?</span></div><h4>NO WORLD ANALYZED</h4><p>Select a Survival world and run the world diagnostic.</p></div>
    </div>
  </div>
  <div class=""footer"">BACKUP-FIRST UTILITY &middot; PLAYER INVENTORIES / BUILDS / QUESTS / PLAYERS ARE NEVER EDITED</div>
</div>
</div>
<div class=""scroll-track"" id=""scrollTrack""><div class=""scroll-thumb"" id=""scrollThumb""></div></div>

<div class=""secret-mods-layer"" id=""secretModsLayer"" onclick=""secretModsBackdropClick(event)"">
  <div class=""secret-mods-panel"" role=""dialog"" aria-modal=""true"" aria-labelledby=""secretModsTitle"">
    <div class=""secret-mods-hazard""></div>
    <div class=""secret-mods-head"">
      <div class=""secret-mods-mark"">
        <svg viewBox=""0 0 64 64"" aria-hidden=""true"" focusable=""false"">
          <polygon class=""secret-mods-mark-shadow"" points=""32,2 62,32 32,62 2,32""></polygon>
          <polygon class=""secret-mods-mark-rim"" points=""32,5 59,32 32,59 5,32""></polygon>
          <polygon class=""secret-mods-mark-face"" points=""32,9 55,32 32,55 9,32""></polygon>
          <path class=""secret-mods-letter-highlight"" transform=""translate(8 8.75) scale(.75)"" d=""M45 17H23L18 22V31L23 36H37L39 38V41L37 43H19V50H41L46 45V36L41 31H27L25 29V26L27 24H45Z""></path>
          <path class=""secret-mods-letter-face"" transform=""translate(8 8) scale(.75)"" d=""M45 17H23L18 22V31L23 36H37L39 38V41L37 43H19V50H41L46 45V36L41 31H27L25 29V26L27 24H45Z""></path>
        </svg>
      </div>
      <div class=""secret-mods-heading""><strong id=""secretModsTitle"">SUPER SECRET MODS</strong><span>EXPERIMENTAL PATCH BAY · AUTHORIZED MECHANICS ONLY</span></div>
      <button type=""button"" class=""secret-mods-close"" aria-label=""Close secret mods"" onclick=""closeSecretMods()"">&times;</button>
    </div>
    <div class=""secret-mods-body"">
      <div class=""secret-mods-warning"">Optional game patches. Save repair stays separate; verified backups rotate automatically.</div>
      <div class=""secret-mod-row secret-master-row"" id=""secretModsMasterRow"">
        <div class=""secret-mod-copy""><strong>SUPER SECRET MODS</strong><span>Master switch for experimental patches added to this hidden patch bay.</span></div>
        <button type=""button"" class=""secret-switch"" id=""secretModsMaster"" role=""switch"" aria-checked=""false"" aria-label=""Toggle Super Secret Mods"" onclick=""toggleSecretModsEnabled()"">
          <span class=""secret-switch-track""></span><span class=""secret-switch-knob""></span>
        </button>
      </div>
      <div class=""secret-mods-catalog-head"">
        <div class=""secret-mods-catalog-label""><b>PATCH CATALOG</b><span id=""secretModCount"">0 ACTIVE &middot; 5 AVAILABLE</span></div>
        <div class=""secret-mod-search""><input type=""text"" id=""secretModSearch"" aria-label=""Filter secret mods"" placeholder=""FILTER MODS..."" onkeyup=""filterSecretMods()"" /></div>
      </div>
      <div class=""secret-mods-list"" id=""secretModsList"">
        <div class=""secret-mod-row secret-mod-card locked"" id=""developerCommandsRow"" data-search=""developer dev commands cheats unlimited god spawn time raid host chat utility"">
          <div class=""secret-mod-copy""><span class=""secret-mod-tag"">COMMAND TOOLS &middot; WORLD-ALTERING</span><strong>DEVELOPER COMMANDS</strong><span>Unlock built-in Survival commands such as /unlimited, with configurable player access.</span><em id=""developerCommandsState"">NOT INSTALLED</em><span class=""secret-compat-reason"" id=""developerCommandsReason""></span></div>
          <div class=""secret-mod-actions"">
            <button type=""button"" class=""secret-mod-options"" id=""developerCommandsOptions"" aria-label=""Open Developer Commands options"" onclick=""openDeveloperCommandOptions()"" disabled=""disabled"">OPTIONS</button>
            <button type=""button"" class=""secret-switch"" id=""developerCommandsSwitch"" role=""switch"" aria-checked=""false"" aria-label=""Toggle Developer Commands"" onclick=""toggleDeveloperCommandsMod()"" disabled=""disabled"">
              <span class=""secret-switch-track""></span><span class=""secret-switch-knob""></span>
            </button>
          </div>
        </div>
        <div class=""secret-mod-row secret-mod-card locked"" id=""resourceLocatorRow"" data-search=""resource locator dots haybot spine wood stone metal connect tool utility"">
          <div class=""secret-mod-copy""><span class=""secret-mod-tag"">UTILITY &middot; CONNECT TOOL</span><strong>RESOURCE LOCATOR DOTS</strong><span id=""resourceLocatorDescription"">Reveal haybot spines and refineable resource cores with an inactive Connect Tool output.</span><em id=""resourceLocatorState"">NOT INSTALLED</em><span class=""secret-compat-reason"" id=""resourceLocatorReason""></span></div>
          <button type=""button"" class=""secret-switch"" id=""resourceLocatorSwitch"" role=""switch"" aria-checked=""false"" aria-label=""Toggle Resource Locator Dots"" onclick=""toggleResourceLocatorMod()"" disabled=""disabled"">
            <span class=""secret-switch-track""></span><span class=""secret-switch-knob""></span>
          </button>
        </div>
        <div class=""secret-mod-row secret-mod-card locked"" id=""revivalBuffRow"" data-search=""revival baguette buffs pizza veggie burger perk knockout death revive multiplayer survival"">
          <div class=""secret-mod-copy""><span class=""secret-mod-tag"">SURVIVAL &middot; REVIVAL</span><strong>REVIVAL BUFF RECOVERY</strong><span>Revival Baguettes restore every pizza and veggie-burger buff held when the player was knocked out.</span><em id=""revivalBuffState"">NOT INSTALLED</em><span class=""secret-compat-reason"" id=""revivalBuffReason""></span></div>
          <button type=""button"" class=""secret-switch"" id=""revivalBuffSwitch"" role=""switch"" aria-checked=""false"" aria-label=""Toggle Revival Buff Recovery"" onclick=""toggleRevivalBuffMod()"" disabled=""disabled"">
            <span class=""secret-switch-track""></span><span class=""secret-switch-knob""></span>
          </button>
        </div>
        <div class=""secret-mod-row secret-mod-card locked"" id=""chemicalFertilizerRow"" data-search=""chemical fertilizer splash farm plot grow bed red farmbot projectile farming dependency"">
          <div class=""secret-mod-copy""><span class=""secret-mod-tag"">FARMING &middot; PROJECTILES</span><strong>CHEMICAL FERTILIZER SPLASH</strong><span>Chemical shots fertilize plots; Red Farmbot pesticide fertilizes a 2.5-block radius.</span><em id=""chemicalFertilizerState"">NOT INSTALLED</em><span class=""secret-compat-reason"" id=""chemicalFertilizerReason""></span></div>
          <button type=""button"" class=""secret-switch"" id=""chemicalFertilizerSwitch"" role=""switch"" aria-checked=""false"" aria-label=""Toggle Chemical Fertilizer Splash"" onclick=""toggleChemicalFertilizerMod()"" disabled=""disabled"">
            <span class=""secret-switch-track""></span><span class=""secret-switch-knob""></span>
          </button>
        </div>
        <div class=""secret-mod-row secret-mod-card locked"" id=""dualFluidCannonRow"" data-search=""dual fluid water cannon chemical container logic projectile farming dependency dangerous save"">
          <div class=""secret-mod-copy""><span class=""secret-mod-tag"">MACHINERY &middot; SAVE-SENSITIVE</span><strong>DUAL-FLUID WATER CANNON</strong><span>Connect Water and Chemical Containers; each logic pulse fires every available liquid.</span><em id=""dualFluidCannonState"">NOT INSTALLED</em><span class=""secret-compat-reason"" id=""dualFluidCannonReason""></span></div>
          <button type=""button"" class=""secret-switch"" id=""dualFluidCannonSwitch"" role=""switch"" aria-checked=""false"" aria-label=""Toggle Dual-Fluid Water Cannon"" onclick=""toggleDualFluidCannonMod()"" disabled=""disabled"">
            <span class=""secret-switch-track""></span><span class=""secret-switch-knob""></span>
          </button>
        </div>
        <div class=""secret-mods-empty"" id=""secretModsEmpty"">NO PATCHES MATCH THIS FILTER</div>
      </div>
      <div class=""secret-mods-feedback"" id=""secretModsFeedback""></div>
      <div class=""secret-mods-slots""><b>DEPENDENCY-SAFE CATALOG</b>Linked patches install and roll back together; risky removals require confirmation.</div>
      <div class=""secret-mods-status"" id=""secretModsStatus""><i></i><span>SECRET PATCH SYSTEM OFFLINE</span></div>
    </div>
  </div>
</div>

<div class=""hotfix-modal item-clear-modal"" id=""itemClearModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""itemClearTitle"" onclick=""itemClearBackdropClick(event)"">
  <div class=""hotfix-dialog"">
    <div class=""hotfix-hazard""></div>
    <div class=""hotfix-head"">
      <div class=""hotfix-alert""><span>!</span></div>
      <div class=""hotfix-title""><strong id=""itemClearTitle"">REMOVE LOOSE WORLD ITEM?</strong><span id=""itemClearKicker"">BACKUP-FIRST WORLD CLEANUP</span></div>
    </div>
    <div class=""hotfix-body"">
      <div class=""item-confirm-preview"" id=""itemClearPreview""></div>
      <p class=""hotfix-intro"" id=""itemClearIntro"">ScrapLab will remove only the selected loose pickup and its matching Lua storage record.</p>
      <ul class=""hotfix-checks"">
        <li>A timestamped database backup is created and integrity-checked first.</li>
        <li>Player inventories, builds, containers, quests, terrain, and raid storage are left alone.</li>
        <li>The removed item can be recovered only by restoring the backup.</li>
      </ul>
      <div class=""hotfix-stop"">SCRAP MECHANIC MUST REMAIN CLOSED UNTIL VERIFICATION FINISHES.</div>
    </div>
    <div class=""hotfix-foot"">
      <div class=""hotfix-foot-note"">Cancel if this is not the correct world or pickup.</div>
      <div class=""hotfix-buttons"">
        <button type=""button"" class=""btn"" onclick=""closeItemClearConfirm()"">CANCEL</button>
        <button type=""button"" class=""btn hotfix-confirm"" id=""itemClearConfirmButton"" onclick=""confirmDroppedItemClear()""><span>!</span>REMOVE THIS DROP</button>
      </div>
    </div>
  </div>
</div>

<div class=""hotfix-modal item-summary-modal"" id=""itemSummaryModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""itemSummaryTitle"" onclick=""itemSummaryBackdropClick(event)"">
  <div class=""hotfix-dialog"">
    <div class=""hotfix-hazard""></div>
    <div class=""hotfix-head"">
      <div class=""item-summary-emblem""><svg viewBox=""0 0 64 64"" width=""58"" height=""58"" aria-hidden=""true""><polygon points=""32,3 61,32 32,61 3,32""></polygon><text x=""32"" y=""39"">&Sigma;</text></svg></div>
      <div class=""hotfix-title""><strong id=""itemSummaryTitle"">LOOSE ITEM TOTALS</strong><span>VALUE-SORTED WORLD INVENTORY REPORT</span></div>
    </div>
    <div class=""hotfix-body"">
      <div class=""item-summary-stats"" id=""itemSummaryStats""></div>
      <div class=""item-summary-scroll-shell""><div class=""item-summary-list"" id=""itemSummaryList""></div><div class=""item-summary-scroll-track"" id=""itemSummaryScrollTrack"" onmousedown=""itemSummaryTrackDown(event)""><div class=""item-summary-scroll-thumb"" id=""itemSummaryScrollThumb"" onmousedown=""itemSummaryThumbDown(event)""></div></div></div>
    </div>
    <div class=""hotfix-foot"">
      <div class=""hotfix-foot-note"">Counts include only decoded loose pickups, not containers, player inventories, or placed creations.</div>
      <div class=""hotfix-buttons""><button type=""button"" class=""btn btn-primary"" onclick=""closeItemSummary()"">CLOSE REPORT</button></div>
    </div>
  </div>
</div>

<div class=""hotfix-modal cannon-danger-modal"" id=""cannonDangerModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""cannonDangerTitle"" onclick=""cannonDangerBackdropClick(event)"">
  <div class=""hotfix-dialog"">
    <div class=""hotfix-hazard""></div>
    <div class=""hotfix-head"">
      <div class=""hotfix-alert""><span>!</span></div>
      <div class=""hotfix-title""><strong id=""cannonDangerTitle"">CREATION / SAVE COMPATIBILITY DANGER</strong><span id=""cannonDangerKicker"">DUAL-FLUID WATER CANNON REMOVAL</span></div>
    </div>
    <div class=""hotfix-body"">
      <p class=""hotfix-intro"">Worlds saved with a Chemical Container connected to a mounted water cannon can fail to load correctly after the original two-input cannon script is restored.</p>
      <ul class=""hotfix-checks"">
        <li>Cancel this warning and launch Scrap Mechanic while the mod is still installed.</li>
        <li>Disconnect every Chemical Container wire from every mounted water cannon.</li>
        <li>Save each affected world, exit to Windows, and wait for ScrapLab to unlock.</li>
      </ul>
      <div class=""hotfix-stop"">DO NOT CONTINUE UNTIL EVERY CHEMICAL CANNON CONNECTION IS REMOVED AND THE WORLD IS SAVED.</div>
      <div class=""cannon-danger-ack"">
        <label><input type=""checkbox"" id=""cannonDangerAck"" onchange=""updateCannonDangerConfirm()"" /><span class=""cannon-danger-box""></span><span>I disconnected every Chemical Container from mounted water cannons and saved all affected worlds.</span></label>
      </div>
    </div>
    <div class=""hotfix-foot"">
      <div class=""hotfix-foot-note"">Cancel is the safe choice if you are not completely sure.</div>
      <div class=""hotfix-buttons"">
        <button type=""button"" class=""btn"" id=""cannonDangerCancel"" onclick=""closeCannonDangerConfirm()"">CANCEL</button>
        <button type=""button"" class=""btn hotfix-confirm"" id=""cannonDangerConfirmButton"" onclick=""confirmCannonDangerChange()"" disabled=""disabled""><span>!</span>DISABLE CANNON MOD</button>
      </div>
    </div>
  </div>
</div>

<div class=""hotfix-modal developer-command-modal"" id=""developerCommandModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""developerCommandTitle"" onclick=""developerCommandBackdropClick(event)"">
  <div class=""hotfix-dialog"">
    <div class=""hotfix-hazard""></div>
    <div class=""hotfix-head"">
      <div class=""hotfix-alert""><span>!</span></div>
      <div class=""hotfix-title""><strong id=""developerCommandTitle"">DEVELOPER COMMAND OPTIONS</strong><span>PLAYER ACCESS CONTROL &middot; BUILT-IN SURVIVAL TOOLS</span></div>
    </div>
    <div class=""hotfix-body"">
      <p class=""hotfix-intro"" id=""developerCommandIntro"">Choose who receives the built-in developer command list when you host a Survival world.</p>
      <div class=""command-access-grid"">
        <button type=""button"" class=""command-access-option selected"" id=""developerModeHost"" onclick=""selectDeveloperCommandMode('host')"">
          <span class=""command-access-bolt""></span><strong>HOST ONLY</strong><small>RECOMMENDED</small><span>Only the world host can use commands. Joined players receive normal Survival controls.</span>
        </button>
        <button type=""button"" class=""command-access-option"" id=""developerModeEveryone"" onclick=""selectDeveloperCommandMode('everyone')"">
          <span class=""command-access-bolt""></span><strong>EVERY PLAYER</strong><small>HIGH TRUST REQUIRED</small><span>Every joined player can use world-changing developer commands while connected.</span>
        </button>
      </div>
      <ul class=""hotfix-checks"">
        <li>Includes /unlimited, /god, /spawn, time controls, item commands, and raid utilities.</li>
        <li>/kick and /ban remain restricted to the host in both access modes.</li>
        <li>Installing or changing access edits no save data; commands players run can permanently change the world.</li>
      </ul>
      <div class=""command-everyone-warning"" id=""developerEveryoneWarning"">
        <div class=""hotfix-stop"">ANY JOINED PLAYER COULD SPAWN UNITS, CHANGE RAIDS OR TIME, GRANT ITEMS, OR ALTER OTHER WORLD STATE.</div>
        <div class=""command-access-ack""><label><input type=""checkbox"" id=""developerEveryoneAck"" onchange=""updateDeveloperCommandOptionButton()"" /><span class=""command-access-box""></span><span>I trust every player who may join and understand their commands can permanently change the world.</span></label></div>
      </div>
    </div>
    <div class=""hotfix-foot"">
      <div class=""hotfix-foot-note"" id=""developerCommandFootNote"">Close Scrap Mechanic before applying this access mode.</div>
      <div class=""hotfix-buttons"">
        <button type=""button"" class=""btn"" onclick=""closeDeveloperCommandConfirm()"">CANCEL</button>
        <button type=""button"" class=""btn hotfix-confirm"" id=""developerCommandConfirmButton"" onclick=""applyDeveloperCommandOptions()""><span>!</span>INSTALL HOST ONLY</button>
      </div>
    </div>
  </div>
</div>

<div class=""hotfix-modal dependency-modal"" id=""dependencyModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""dependencyTitle"" onclick=""dependencyBackdropClick(event)"">
  <div class=""hotfix-dialog"">
    <div class=""hotfix-hazard""></div>
    <div class=""hotfix-head"">
      <div class=""hotfix-alert""><span>!</span></div>
      <div class=""hotfix-title""><strong id=""dependencyTitle"">DEPENDENCY CHANGE REQUIRED</strong><span id=""dependencyKicker"">DUAL-FLUID PATCH COORDINATOR</span></div>
    </div>
    <div class=""hotfix-body"">
      <p class=""hotfix-intro"" id=""dependencyIntro"">This action changes two linked secret mods.</p>
      <ul class=""hotfix-checks"">
        <li id=""dependencyFirstChange"">The required dependency will be changed first.</li>
        <li id=""dependencySecondChange"">The requested cannon patch will be changed second.</li>
        <li>Both operations create checksum-verified backups and roll back together if either one fails.</li>
      </ul>
      <div class=""hotfix-stop"">SCRAP MECHANIC MUST BE COMPLETELY CLOSED BEFORE CHANGING THESE PATCHES.</div>
    </div>
    <div class=""hotfix-foot"">
      <div class=""hotfix-foot-note"">One Windows administrator request covers the complete dependency operation.</div>
      <div class=""hotfix-buttons"">
        <button type=""button"" class=""btn"" onclick=""closeDependencyConfirm()"">CANCEL</button>
        <button type=""button"" class=""btn hotfix-confirm"" id=""dependencyConfirmButton"" onclick=""confirmDependencyChange()""><span>!</span>CONFIRM CHANGES</button>
      </div>
    </div>
  </div>
</div>

<div class=""onboard-modal"" id=""onboardModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""onboardTitle"">
  <div class=""onboard-dialog"">
    <div class=""onboard-hazard""></div>
    <div class=""onboard-main"">
      <div class=""onboard-mark""><span>?</span></div>
      <div class=""onboard-copy"">
        <div class=""onboard-kicker"">FIRST START &middot; RECOVERY ASSISTANT</div>
        <h2 id=""onboardTitle"">WOULD YOU LIKE A QUICK TUTORIAL?</h2>
        <p>An interactive tour will show you how to choose the correct world, inspect raids and loose items, make a safe backup-first repair, and use the temporary game hotfix.</p>
      </div>
    </div>
    <div class=""onboard-preview"">
      <div>No technical knowledge required</div>
      <div>Nothing is changed during the tour</div>
      <div>Replay it anytime from Help</div>
    </div>
    <div class=""onboard-actions"">
      <p><b>Important:</b> Scrap Mechanic must be closed before ScrapLab opens a world save.</p>
      <div><button type=""button"" class=""btn"" onclick=""declineTutorial()"">NOT NOW</button><button type=""button"" class=""btn btn-primary"" id=""onboardStart"" onclick=""acceptTutorial()"">START TUTORIAL</button></div>
    </div>
  </div>
</div>

<div class=""help-modal"" id=""helpModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""helpTitle"" onclick=""helpBackdropClick(event)"">
  <div class=""help-dialog"">
    <div class=""help-hazard""></div>
    <div class=""help-head"">
      <div class=""help-emblem"">?</div>
      <div class=""help-heading""><strong id=""helpTitle"">SCRAP LAB FIELD MANUAL</strong><span>WORLD INSPECTION &middot; PERFORMANCE &middot; RECOVERY &middot; MOD WORKSHOP</span></div>
      <button type=""button"" class=""help-close"" title=""Close help"" aria-label=""Close help"" onclick=""closeHelp()"">&times;</button>
    </div>
    <div class=""help-body"" id=""helpBody"">
      <div class=""help-quick"">
        <div class=""help-step""><b>01</b><span>Close Scrap Mechanic completely.</span></div>
        <div class=""help-step""><b>02</b><span>Select the correct Survival world.</span></div>
        <div class=""help-step""><b>03</b><span>Analyze raids, then scan loose items if needed.</span></div>
        <div class=""help-step""><b>04</b><span>Repair only after checking the results.</span></div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">CHOOSING AND ANALYZING A WORLD</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>AUTOMATIC SAVE LIST</b><p>ScrapLab searches every Scrap Mechanic <strong>User_*</strong> Survival folder and puts the newest saves first. Check the world name, date, size, and user folder before continuing.</p></div>
          <div class=""help-item""><b>BROWSE</b><p>Use Browse only when the save is not listed automatically. Choose the world’s normal <strong>.db</strong> file, not a backup file.</p></div>
          <div class=""help-item""><b>ANALYZE WORLD</b><p>This read-only check validates SQLite health and decodes the raid manager. Loose pickups stay unloaded until you choose <strong>Scan Loose Items</strong>.</p></div>
          <div class=""help-item""><b>LIVE-SAVE SAFETY LOCK</b><p>If Scrap Mechanic is running, world controls lock. A second native check happens immediately before SQLite opens, preventing stale or fast clicks from reaching the live database.</p></div>
        </div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">LOOSE WORLD ITEMS</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>OPTIONAL SCAN</b><p>Click <strong>Scan Loose Items</strong> after analyzing a world. Pickups are then ordered by value and shown with their real Scrap Mechanic icon, stack quantity, world, coordinates, and remaining lifetime.</p></div>
          <div class=""help-item""><b>ITEM TOTALS</b><p>Open <strong>Item Totals</strong> for one combined quantity per item type, plus unique-item, loose-stack, and expired-stack totals.</p></div>
          <div class=""help-item""><b>VALUE ORDER</b><p>Progression and quest items rank first. Crafted parts use the installed game's recipe ingredients; consumables, crops, materials, common objects, and unknown modded items receive safe category fallbacks.</p></div>
          <div class=""help-item""><b>WHAT IS NOT LISTED</b><p>Placed blocks, vehicle parts, player inventories, containers, quest reward objects, and loot attached to scenery are not treated as ordinary loose pickups.</p></div>
          <div class=""help-item""><b>REMOVE ONE</b><p>Use <strong>Remove Item</strong> on a card to delete only that pickup's harvestable entity and matching Lua-storage row.</p></div>
          <div class=""help-item""><b>CLEANUP OPTIONS</b><p><strong>Clear Expired</strong> removes only pickups marked Pending World Cleanup. <strong>Clear All Dropped Items</strong> removes every safely decoded loose pickup shown. Unreadable or ambiguous records are always skipped.</p></div>
          <div class=""help-item""><b>EXPIRY TIMER</b><p>Normal loose loot lasts one in-game hour. The displayed countdown is calculated from the saved game tick; it advances only while the world is running.</p></div>
          <div class=""help-item""><b>BACKUP AND VERIFICATION</b><p>Every cleanup option creates a timestamped verified backup, uses one SQLite transaction, preserves raid storage, and re-analyzes the edited save before reporting success.</p></div>
        </div>
        <div class=""help-danger""><b>REMOVAL IS A REAL SAVE EDIT.</b> Confirm the correct world and item first. Keep the generated backup until the world has loaded and saved normally.</div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">UNDERSTANDING RAID DIAGNOSTICS</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>DATABASE HEALTH / SAVE VERSION</b><p><strong>Healthy</strong> means SQLite passed its integrity check. Unsupported legacy saves are never edited.</p></div>
          <div class=""help-item""><b>TIER, THREAT, AND STATE</b><p>Tier shows raid difficulty. Threat shows raid progress. State and timing values help identify a raid that stopped advancing.</p></div>
          <div class=""help-item""><b>ENEMIES AND CROPS</b><p>Planned enemies show the raid composition. Stored crop references show what originally contributed to the raid calculation.</p></div>
          <div class=""help-item""><b>PERMANENT RAID WARNING</b><p>A likely stuck raid is flagged when saved crop references no longer match live harvestables while the raid record remains active.</p></div>
        </div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">RESOLVE &amp; CLEAR RAIDS — SAVE REPAIR</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>WHAT IT CHANGES</b><p>It first releases the exact growing crops registered to the stored raids, then removes the base-game raid-manager record in the same transaction.</p></div>
          <div class=""help-item""><b>WHAT IT LEAVES ALONE</b><p>Inventories, builds, quests, players, containers, terrain, and unrelated script data are not edited.</p></div>
          <div class=""help-item""><b>BACKUP FIRST</b><p>A timestamped copy is created beside the save and verified before repair. The repaired database must also pass a final integrity check.</p></div>
          <div class=""help-item""><b>ORPHANED CROPS</b><p>If an older Raid Rescue build removed a raid without releasing its crops, Repair Orphaned Crops safely releases only crops no longer referenced by an active raid.</p></div>
        </div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">SUPER SECRET MODS &mdash; BACKUPS AND REMOVAL</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>ADAPTIVE GAME UPDATES</b><p>After Steam installs a new build, secret-mod switches turn off until you intentionally re-enable them. If the protected code is still exact, ScrapLab safely refreshes the generated script cache without rewriting unchanged Lua.</p></div>
          <div class=""help-item""><b>PROTECTED CODE</b><p>Formatting or comments inside required code block the patch. Unrelated updated code elsewhere is preserved. The normal cumulative raid/fertilizer hotfix remains strictly version-locked.</p></div>
          <div class=""help-item""><b>HYBRID RESTORATION</b><p>Unchanged adaptive installs restore their exact pre-install bytes. If unrelated edits were added later, ScrapLab removes only its intact snippets. Edited, duplicated, or partial patch snippets block removal safely.</p></div>
          <div class=""help-item""><b>HOTFIX INDEPENDENCE</b><p>Chemical Fertilizer removal restores the exact state from before that mod. If the separate cumulative fertilizer hotfix was installed, that normal hotfix remains installed.</p></div>
          <div class=""help-item""><b>BLOCKED STATES</b><p><strong>Required Code Changed</strong>, <strong>Other Modification Detected</strong>, and <strong>Partial Patch</strong> identify unsafe files without writing. Steam Verify can restore official files, but changed protected features may require a ScrapLab update.</p></div>
          <div class=""help-item""><b>BOUNDED RETENTION</b><p>ScrapLab keeps the two newest verified backups for each install, remove, or configure action. Superseded copies are removed only after a patch and its checksum verification succeed.</p></div>
          <div class=""help-item""><b>UNKNOWN FOLDERS ARE SAFE</b><p>Backup cleanup recognizes only ScrapLab and legacy Raid Rescue timestamped secret-mod folders. Other folders and manual backups are never removed.</p></div>
        </div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">SUPER SECRET MODS &mdash; REVIVAL BUFF RECOVERY</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>REVIVAL BAGUETTES ONLY</b><p>When a knocked-out player is revived with a real Revival Baguette, every active food buff they held at the moment of knockout returns with them.</p></div>
          <div class=""help-item""><b>ALL FOOD BUFFS</b><p>The exact pizza and veggie-burger perk set is preserved, including maximum health, hammer speed, fall protection, and high jump. No random replacement buff is granted.</p></div>
          <div class=""help-item""><b>NORMAL RESPAWNS STAY NORMAL</b><p>Choosing a normal respawn or using a forced revival still clears the recovery snapshot. Buffs cannot leak into a later life or an unrelated revival.</p></div>
          <div class=""help-item""><b>MULTIPLAYER SAFE</b><p>The host records each player independently. A player who disconnects while knocked out can reconnect and still receive their own saved buffs when revived with a baguette.</p></div>
        </div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">SUPER SECRET MODS &mdash; DEVELOPER COMMANDS</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>HOW TO USE</b><p>Open <strong>Options</strong>, choose an access mode, and install with Scrap Mechanic closed. In a hosted Survival world, open chat and enter a command such as <strong>/unlimited</strong>. Use <strong>/limited</strong> to return to normal inventory rules.</p></div>
          <div class=""help-item""><b>HOST ONLY</b><p>The recommended mode registers commands only for the world host. Joined players keep normal Survival controls.</p></div>
          <div class=""help-item""><b>EVERY PLAYER</b><p>Every joined player receives the command list while connected. Use this only with players you completely trust. <strong>/kick</strong> and <strong>/ban</strong> stay host-only.</p></div>
          <div class=""help-item""><b>AVAILABLE TOOLS</b><p>Built-in commands cover items and weapons, god mode, inventory mode, unit spawning, time controls, player values, aggro, raids, starter kits, and unstuck tools.</p></div>
          <div class=""help-item""><b>NOT SERVER DEV MODE</b><p>ScrapLab does not enable the server-side <strong>g_survivalDev</strong> mode, so normal world spawn points and progression remain active. Every Player mode sends the client permission needed to register commands and may expose other client-only developer behavior during that session.</p></div>
        </div>
        <div class=""help-danger""><b>COMMANDS CAN PERMANENTLY CHANGE YOUR WORLD.</b> Back up important saves before experimenting. In Every Player mode, any joined player can make these changes. ScrapLab verifies and backs up the game script, but it cannot undo effects produced by commands.</div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">SUPER SECRET MODS — DUAL-FLUID CANNON</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>CONNECTING THE CANNON</b><p>Use the Connect Tool to attach one logic source, one Water Container, and one Chemical Container. The three connections may be added in any order.</p></div>
          <div class=""help-item""><b>HOW IT FIRES</b><p>Each OFF-to-ON logic pulse fires every liquid currently available. Water uses the connected container first, then the cannon’s original water-only internal tank.</p></div>
          <div class=""help-item""><b>FERTILIZER DEPENDENCY</b><p>Dual-Fluid Water Cannon requires <strong>Chemical Fertilizer Splash</strong>. ScrapLab can install both together and always removes the cannon before removing that dependency.</p></div>
          <div class=""help-item""><b>EMPTY CONTAINERS</b><p>An empty liquid does not block the other one. With both supplied, one water and one chemical are consumed and their projectiles leave the same muzzle together.</p></div>
        </div>
        <div class=""help-danger""><b>REMOVE CONNECTIONS BEFORE DISABLING.</b> While the cannon mod is still installed, disconnect every Chemical Container from every mounted water cannon, save each affected world, and close the game. Only then disable the cannon mod. Steam Verify or a game update can also restore the original two-input script, so disconnect and save before using either one.</div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">SCRAP LAB UPDATES</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>AUTOMATIC CHECKS</b><p>ScrapLab checks the official GitHub release shortly after startup and every 30 minutes while the app remains open. Network failures stay quiet so save repair is never interrupted.</p></div>
          <div class=""help-item""><b>ONE-CLICK UPDATE</b><p>Choose <strong>Update + Restart</strong> to download the official executable, verify GitHub's SHA-256 digest and version, replace this copy, and reopen automatically.</p></div>
          <div class=""help-item""><b>SAFE REPLACEMENT</b><p>The updater keeps one bounded previous-executable backup, verifies the replacement again, and rolls back if installation or relaunch fails.</p></div>
          <div class=""help-item""><b>INSTALLED VERSION</b><p>This copy is <strong id=""helpAppVersion"">loading...</strong>. Use <strong>Check Updates</strong> below whenever you want an immediate check.</p></div>
        </div>
      </div>

      <div class=""help-section"">
        <div class=""help-section-title"">COMMON PROBLEMS</div>
        <div class=""help-grid"">
          <div class=""help-item""><b>THE GAME LOOKS CLOSED BUT CONTROLS STAY LOCKED</b><p>Wait a moment for automatic detection. If needed, open Task Manager and confirm ScrapMechanic.exe and ScrapMechanicServer.exe have exited.</p></div>
          <div class=""help-item""><b>MY SAVE IS NOT LISTED</b><p>Confirm it is a Chapter 2 Survival save, then use Browse. Steam Cloud or another Windows account may store the world under a different User_* folder.</p></div>
          <div class=""help-item""><b>ANTIVIRUS WARNING</b><p>Small unsigned portable utilities can trigger reputation or machine-learning warnings. Download only from the official GitHub release and compare its published SHA-256 checksum.</p></div>
          <div class=""help-item""><b>NEED TO RESTORE</b><p>Close the game, keep the broken file somewhere safe, and copy the timestamped ScrapLab backup back to the original filename and Survival folder.</p></div>
        </div>
      </div>
    </div>
    <div class=""help-foot"">
      <div class=""help-status"" id=""helpStatus"">Tutorial controls are available here whenever you need them.</div>
      <div class=""help-buttons""><button type=""button"" class=""btn"" id=""helpUpdateButton"" onclick=""checkForUpdates(true)"">CHECK UPDATES</button><button type=""button"" class=""btn"" onclick=""resetTutorialPrompt()"">RESET FIRST-RUN PROMPT</button><button type=""button"" class=""btn btn-primary"" onclick=""replayTutorial()"">REPLAY TUTORIAL</button></div>
    </div>
  </div>
</div>

<div class=""tutorial"" id=""tutorial"" role=""dialog"" aria-modal=""true"" aria-labelledby=""tutorialTitle"">
  <div class=""tutorial-shade"" id=""tutorialShadeTop""></div>
  <div class=""tutorial-shade"" id=""tutorialShadeRight""></div>
  <div class=""tutorial-shade"" id=""tutorialShadeBottom""></div>
  <div class=""tutorial-shade"" id=""tutorialShadeLeft""></div>
  <div class=""tutorial-focus"" id=""tutorialFocus""></div>
  <div class=""tutorial-card"" id=""tutorialCard"">
    <div class=""tutorial-rail""></div>
    <div class=""tutorial-content"">
      <div class=""tutorial-meta"">
        <div class=""tutorial-number"">
          <svg viewBox=""0 0 64 64"" aria-hidden=""true"" focusable=""false"">
            <defs>
              <linearGradient id=""tutorialBadgeFaceGradient"" x1=""0%"" y1=""0%"" x2=""0%"" y2=""100%"">
                <stop offset=""0%"" stop-color=""#fff2a0""></stop>
                <stop offset=""42%"" stop-color=""#ffd046""></stop>
                <stop offset=""100%"" stop-color=""#e39a10""></stop>
              </linearGradient>
            </defs>
            <polygon class=""tutorial-number-shadow"" points=""32,2 62,32 32,62 2,32""></polygon>
            <polygon class=""tutorial-number-mount"" points=""32,3 61,32 32,61 3,32""></polygon>
            <polygon class=""tutorial-number-rim"" points=""32,6 58,32 32,58 6,32""></polygon>
            <polygon class=""tutorial-number-face"" points=""32,10 54,32 32,54 10,32""></polygon>
            <polygon class=""tutorial-number-inset"" points=""32,13 51,32 32,51 13,32""></polygon>
            <path class=""tutorial-number-highlight"" d=""M12 30 L32 10 L52 30""></path>
            <path class=""tutorial-number-shade"" d=""M52 34 L32 54 L12 34""></path>
            <text class=""tutorial-number-text"" id=""tutorialNumber"" x=""32"" y=""38"">01</text>
          </svg>
        </div>
        <div><div class=""tutorial-label"" id=""tutorialLabel"">GUIDED WORLD TOUR</div><div class=""tutorial-title"" id=""tutorialTitle"">WELCOME TO SCRAP LAB</div></div>
      </div>
      <p class=""tutorial-text"" id=""tutorialText""></p>
      <div class=""tutorial-tip"" id=""tutorialTip""></div>
    </div>
    <div class=""tutorial-progress"" id=""tutorialProgress""></div>
    <div class=""tutorial-actions"">
      <div class=""tutorial-actions-left""><button type=""button"" class=""btn"" onclick=""skipTutorial()"">EXIT TOUR</button><button type=""button"" class=""btn"" id=""tutorialBack"" onclick=""previousTutorialStep()"">BACK</button></div>
      <button type=""button"" class=""btn btn-primary tutorial-next"" id=""tutorialNext"" onclick=""nextTutorialStep()"">NEXT</button>
    </div>
  </div>
</div>

<div class=""update-modal"" id=""updateModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""updateTitle"" onclick=""updateBackdropClick(event)"">
  <div class=""update-dialog"">
    <div class=""update-hazard""></div>
    <div class=""update-head"">
      <div class=""update-emblem"">
        <svg viewBox=""0 0 64 64"" aria-hidden=""true"" focusable=""false"">
          <polygon class=""update-emblem-shadow"" points=""32,2 62,32 32,62 2,32""></polygon>
          <polygon class=""update-emblem-rim"" points=""32,4 60,32 32,60 4,32""></polygon>
          <polygon class=""update-emblem-face"" points=""32,9 55,32 32,55 9,32""></polygon>
          <path class=""update-emblem-arrow"" d=""M22 31 L32 41 L42 31 M32 40 L32 19""></path>
        </svg>
      </div>
      <div class=""update-heading""><strong id=""updateTitle"">NEW SCRAP LAB BUILD AVAILABLE</strong><span>OFFICIAL GITHUB RELEASE &middot; VERIFIED SELF-UPDATE</span></div>
    </div>
    <div class=""update-body"">
      <p class=""update-intro"" id=""updateIntro"">A newer toolkit build is ready. ScrapLab can install it and reopen automatically.</p>
      <div class=""update-version-rail"">
        <div class=""update-version-node""><b>INSTALLED UNIT</b><strong id=""updateCurrentVersion"">0.0.0</strong></div>
        <div class=""update-flow""></div>
        <div class=""update-version-node latest""><b>AVAILABLE UNIT</b><strong id=""updateLatestVersion"">0.0.0</strong></div>
      </div>
      <div class=""update-proof"">
        <div><b>OFFICIAL SOURCE</b>Only Cooperkit ScrapLab release assets are accepted.</div>
        <div><b>SHA-256 LOCK</b>The download must match GitHub's published digest.</div>
        <div><b>SAFE RELAUNCH</b>One previous EXE is kept for automatic rollback.</div>
      </div>
      <div class=""update-status"" id=""updateStatus"">Ready to download and verify the new toolkit build.</div>
      <div class=""update-progress""><span id=""updateProgressFill""></span></div>
    </div>
    <div class=""update-foot"">
      <div class=""update-foot-note"">Updating ScrapLab does not open, modify, or reinstall any Scrap Mechanic save or game patch.</div>
      <div class=""update-buttons"">
        <button type=""button"" class=""btn"" id=""updateLaterButton"" onclick=""closeUpdateModal()"">LATER</button>
        <button type=""button"" class=""btn"" id=""updateReleaseButton"" onclick=""openUpdateRelease()"">VIEW RELEASE</button>
        <button type=""button"" class=""btn update-install"" id=""updateInstallButton"" onclick=""installAppUpdate()"">UPDATE + RESTART</button>
      </div>
    </div>
  </div>
</div>

<div class=""hotfix-modal"" id=""hotfixModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""hotfixTitle"" onclick=""hotfixBackdropClick(event)"">
  <div class=""hotfix-dialog"">
    <div class=""hotfix-hazard""></div>
    <div class=""hotfix-head"">
      <div class=""hotfix-alert""><span>!</span></div>
      <div class=""hotfix-title""><strong id=""hotfixTitle"">SYSTEM MODIFICATION WARNING</strong><span>CUMULATIVE SCRAP MECHANIC 1.0.2 HOTFIX</span></div>
    </div>
    <div class=""hotfix-body"">
      <p class=""hotfix-intro"">ScrapLab is ready to install or update the temporary game hotfix.</p>
      <ul class=""hotfix-checks"">
        <li>Only supported original files or verified ScrapLab versions are accepted.</li>
        <li>Previously installed raid fixes are preserved when new fixes are added.</li>
        <li>A checksum-verified backup is created before any script is changed.</li>
        <li>The cumulative hotfix repairs stuck raids and fertilizer growth timing.</li>
        <li>The generated script cache is reset so changes load without <strong>-dev</strong>.</li>
      </ul>
      <div class=""hotfix-stop"">SCRAP MECHANIC MUST BE COMPLETELY CLOSED BEFORE INSTALLATION.</div>
    </div>
    <div class=""hotfix-foot"">
      <div class=""hotfix-foot-note"">Windows requests administrator permission once per ScrapLab session; later patch actions reuse that protected session.</div>
      <div class=""hotfix-buttons"">
        <button type=""button"" class=""btn"" onclick=""closeHotfixConfirm()"">CANCEL</button>
        <button type=""button"" class=""btn hotfix-confirm"" id=""hotfixConfirmButton"" onclick=""confirmHotfixInstall()""><span>!</span>INSTALL HOTFIX</button>
      </div>
    </div>
  </div>
</div>

<div class=""busy"" id=""busy""><div class=""busy-card""><div class=""busy-icon""><span>!</span></div>
  <strong id=""busyTitle"">READING WORLD DATABASE</strong><p id=""busyText"">Local operation in progress.</p>
  <div class=""loading-status""><span id=""busyPhase"">INITIALIZING</span><b id=""busyPercent"">0%</b></div>
  <div class=""loading-track""><div class=""loading-fill"" id=""busyProgressFill""></div></div>
</div></div>
<div class=""update-toast"" id=""updateToast""><button type=""button"" aria-label=""Close update message"" onclick=""closeUpdateToast()"">&times;</button><b id=""updateToastTitle"">UPDATE STATUS</b><span id=""updateToastText""></span></div>

<script>
var currentPath='';
var lastAnalysis=null;
var lastBackupPath='';
var saveItems=[];
var scrollDrag=false;
var scrollDragY=0;
var scrollDragTop=0;
var scrollUiQueued=false;
var scrollIdleTimer=0;
var smoothScrollRunning=false;
var smoothScrollTarget=0;
var smoothScrollPane=null;
var gameRunning=null;
var operationBusy=false;
var performanceOperationId='';
var performancePollTimer=0;
var performanceStatus=null;
var performanceResult=null;
var performancePath='';
var performanceActive=false;
var performanceWorldFilter='all';
var performanceExplorerOpen=false;
var performanceExplorerPage=null;
var performanceExplorerWorldId=null;
var performanceExplorerOffset=0;
var performanceExplorerLimit=25;
var performanceExportMessage='';
var performanceExportFailed=false;
var busyProgressValue=0;
var busyProgressTimer=0;
var busyHideTimer=0;
var busyProgressToken=0;
var tutorialActive=false;
var tutorialIndex=0;
var tutorialStartScroll=0;
var tutorialResultHtml='';
var tutorialOfferChecks=0;
var secretModsEnabled=false;
var secretResourceLocatorInstalled=false;
var secretResourceLocatorNeedsUpdate=false;
var secretResourceLocatorCompatibility='';
var secretResourceLocatorCanApply=true;
var secretResourceLocatorReason='';
var secretRevivalBuffInstalled=false;
var secretRevivalBuffCompatibility='';
var secretRevivalBuffCanApply=true;
var secretRevivalBuffReason='';
var secretDeveloperCommandsInstalled=false;
var secretDeveloperCommandsError='';
var secretDeveloperCommandsCompatibility='';
var secretDeveloperCommandsCanApply=true;
var secretDeveloperCommandsReason='';
var secretDeveloperCommandsMode='host';
var developerCommandDraftMode='host';
var secretChemicalFertilizerInstalled=false;
var secretChemicalFertilizerCompatibility='';
var secretChemicalFertilizerCanApply=true;
var secretChemicalFertilizerReason='';
var secretDualFluidCannonInstalled=false;
var secretDualFluidCannonError='';
var secretDualFluidCannonCompatibility='';
var secretDualFluidCannonCanApply=true;
var secretDualFluidCannonReason='';
var secretModBusy=false;
var secretModBusyTarget='';
var secretDependencyAction='';
var secretCannonDangerAction='';
var appVersion='';
var updateCheckBusy=false;
var updateCheckManual=false;
var updateInstallBusy=false;
var updateProgressValue=0;
var updateProgressTimer=0;
var updateState=null;
var pendingUpdateState=null;
var updateDismissedTag='';
var updateToastTimer=0;
var pendingDroppedEntityId=0;
var pendingDroppedItem=null;
var pendingDroppedMode='';
var itemSummaryScrollDrag=false;
var itemSummaryScrollDragY=0;
var itemSummaryScrollDragTop=0;
var droppedItemsCollapsed=false;
var tutorialSteps=[
 {target:'identityPanel',badge:'00',label:'GUIDED WORLD TOUR',title:'WELCOME TO SCRAP LAB',
  text:'ScrapLab inspects worlds, finds performance hotspots and loose items, and offers backup-first recovery tools.',
  tip:'This tour only points things out. It changes nothing.'},
 {target:'selectorPanel',badge:'01',label:'STEP 1 — SAFETY FIRST',title:'CLOSE SCRAP MECHANIC',
  text:'Close Scrap Mechanic before opening a save. ScrapLab locks world controls while the game is running.',
  tip:'The controls unlock automatically when the game closes.'},
 {target:'savePicker',badge:'02',label:'STEP 2 — WORLD SELECTION',title:'CHOOSE THE CORRECT SAVE',
  text:'Choose your world from the list and check its name and date. Use Browse only if it is missing.',
  tip:'Pick the normal .db file, not a .scraplab-backup or legacy .raidrescue-backup file.'},
 {target:'analyzeBtn',badge:'03',label:'STEP 3 — READ-ONLY CHECK',title:'ANALYZE BEFORE REPAIRING',
  text:'Analyze World checks database health and stored raids without editing anything.',
  tip:'Loose items are scanned separately only when you request it.'},
 {target:'tutorialRaidExample',fallback:'result',badge:'04',label:'STEP 4 — UNDERSTAND THE REPORT',title:'READ EACH RAID CARD',
  text:'Each card shows the raid tier, threat, robots, crops, and timing. Read any warning before repairing.',
  tip:'A high raid tier by itself does not mean the raid is bugged.'},
 {target:'droppedItemsZone',fallback:'result',badge:'05',label:'STEP 5 — LOOSE ITEM RECOVERY',title:'REVIEW DROPPED WORLD ITEMS',
  text:'Click Scan Loose Items to load pickup icons, totals, positions, and despawn timers.',
  tip:'Both actions create and verify a backup before changing the save.'},
 {target:'clearAllBtn',fallback:'diagnosticsPanel',badge:'06',label:'STEP 6 — SAVE REPAIR',title:'RESOLVE RAIDS SAFELY',
  text:'Resolve & Clear Raids backs up the save, releases its registered crops, then removes the stored raid state.',
  tip:'Active crop growth is preserved while builds, inventory, quests, and players stay untouched.'},
 {target:'repairActionsBar',fallback:'diagnosticsPanel',badge:'07',label:'STEP 7 — BACKUP-FIRST REPAIR',title:'KEEP THE VERIFIED BACKUP',
  text:'Every save repair creates and checks a timestamped backup before the database changes.',
  tip:'Test the repaired world before deleting its backup.'},
 {target:'helpBtn',badge:'08',label:'STEP 8 — HELP IS ALWAYS HERE',title:'OPEN THE FIELD MANUAL',
  text:'Use the ? button anytime for instructions, troubleshooting, and backup help.',
  tip:'You can also replay this tour from the Help menu.'},
 {target:'identityPanel',badge:'OK',label:'TOUR COMPLETE',title:'YOU ARE READY',
  text:'Close the game, choose the world, analyze, review, then repair. Test the world before deleting its backup.',
  tip:'Unsure about anything? Stop and open Help.'}
];

function beginWindowDrag(){window.external.BeginDrag();}
function minimizeWindow(){window.external.Minimize();}
function closeWindow(){if(!updateInstallBusy)window.external.CloseWindow();}
function loadAppUpdateState(){
 try{appVersion=String(window.external.GetAppVersion()||'');}catch(e){appVersion='';}
 var versionNode=document.getElementById('helpAppVersion');
 if(versionNode)versionNode.innerText=appVersion?'version '+appVersion:'an unknown version';
 try{
  var startup=parseResult(window.external.ConsumeUpdateStartupStatus());
  if(startup.HasStatus){
   if(startup.Success)showUpdateToast('SCRAP LAB UPDATED','ScrapLab '+escPlain(startup.Version)+' installed and reopened successfully.','good',7000);
   else showUpdateToast('UPDATE ROLLED BACK',startup.Error||'The previous ScrapLab executable was restored.','bad',9000);
  }
 }catch(e){}
}
function escPlain(value){return value===null||typeof value==='undefined'?'':String(value);}
function updateUiBlocked(){
 if(operationBusy||tutorialActive||updateInstallBusy)return true;
 var ids=['updateModal','onboardModal','helpModal','hotfixModal','itemClearModal','itemSummaryModal','dependencyModal','developerCommandModal','cannonDangerModal','secretModsLayer'];
 for(var i=0;i<ids.length;i++){
  var node=document.getElementById(ids[i]);
  if(node&&String(node.className).indexOf('show')>=0)return true;
 }
 return false;
}
function checkForUpdates(manual){
 manual=!!manual;
 if(updateCheckBusy){
  if(manual)setHelpUpdateStatus('UPDATE CHECK ALREADY RUNNING.','');
  return;
 }
 updateCheckBusy=true;updateCheckManual=manual;
 var button=document.getElementById('helpUpdateButton');
 if(button)button.disabled=true;
 if(manual)setHelpUpdateStatus('CONTACTING THE OFFICIAL GITHUB RELEASE...','');
 var started=false;
 try{started=!!window.external.CheckForUpdates(manual);}catch(e){}
 if(!started){
  updateCheckBusy=false;
  if(button)button.disabled=false;
  if(manual)setHelpUpdateStatus('THE UPDATE SERVICE IS BUSY. TRY AGAIN IN A MOMENT.','bad');
 }
}
function receiveUpdateCheck(text,manual){
 updateCheckBusy=false;
 var button=document.getElementById('helpUpdateButton');
 if(button)button.disabled=false;
 var data=parseResult(text),wasManual=!!manual||updateCheckManual;
 updateCheckManual=false;
 if(!data.Success){
  if(wasManual){
   setHelpUpdateStatus(data.Error||'Could not check GitHub right now.','bad');
   showUpdateToast('UPDATE CHECK FAILED',data.Error||'Could not check GitHub right now.','bad',7000);
  }
  return;
 }
 if(!data.UpdateAvailable){
  if(wasManual){
   setHelpUpdateStatus('SCRAP LAB '+escPlain(data.CurrentVersion)+' IS UP TO DATE.','good');
   showUpdateToast('SCRAP LAB CURRENT','You already have the latest ScrapLab release.','good',4500);
  }
  return;
 }
 updateState=data;
 if(wasManual){
  closeHelp();
  showUpdateModal(data);
 }else if(updateDismissedTag!==String(data.TagName||'')&&!updateUiBlocked()){
  showUpdateModal(data);
 }else{
  pendingUpdateState=data;
 }
}
function setHelpUpdateStatus(message,kind){
 var status=document.getElementById('helpStatus');
 if(!status)return;
 status.className='help-status'+(kind?' '+kind:'');
 status.innerText=message;
}
function showUpdateModal(data){
 if(!data||updateInstallBusy)return;
 pendingUpdateState=null;updateState=data;
 closeSaveMenu();closeHotfixConfirm();closeSecretMods();
 document.getElementById('updateCurrentVersion').innerText=data.CurrentVersion||appVersion||'UNKNOWN';
 document.getElementById('updateLatestVersion').innerText=data.LatestVersion||data.TagName||'NEW';
 var status=document.getElementById('updateStatus');
 status.className=data.CanAutoUpdate?'update-status':'update-status bad';
 status.innerText=data.CanAutoUpdate
  ?'Ready to download, verify, install, and reopen ScrapLab.'
  :(data.Error||'Automatic installation is unavailable for this release.');
 var install=document.getElementById('updateInstallButton');
 install.disabled=!data.CanAutoUpdate;
 install.innerText=data.CanAutoUpdate?'UPDATE + RESTART':'AUTO-UPDATE UNAVAILABLE';
 document.getElementById('updateLaterButton').disabled=false;
 document.getElementById('updateReleaseButton').disabled=false;
 document.getElementById('updateModal').className='update-modal show';
 window.setTimeout(function(){(data.CanAutoUpdate?install:document.getElementById('updateReleaseButton')).focus();},35);
}
function maybeShowPendingUpdate(){
 if(!pendingUpdateState||updateUiBlocked())return;
 if(updateDismissedTag===String(pendingUpdateState.TagName||'')){pendingUpdateState=null;return;}
 showUpdateModal(pendingUpdateState);
}
function updateBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('updateModal'))closeUpdateModal();
}
function closeUpdateModal(){
 if(updateInstallBusy)return;
 if(updateState)updateDismissedTag=String(updateState.TagName||'');
 document.getElementById('updateModal').className='update-modal';
}
function openUpdateRelease(){
 if(!updateState||!updateState.ReleaseUrl)return;
 try{window.external.OpenUpdateRelease(String(updateState.ReleaseUrl));}catch(e){}
}
function setUpdateProgress(value){
 updateProgressValue=Math.max(0,Math.min(100,Number(value)||0));
 var fill=document.getElementById('updateProgressFill');
 if(fill)fill.style.width=Math.round(updateProgressValue)+'%';
}
function startUpdateProgress(){
 if(updateProgressTimer)window.clearInterval(updateProgressTimer);
 setUpdateProgress(4);
 updateProgressTimer=window.setInterval(function(){
  var ceiling=92;
  if(updateProgressValue>=ceiling)return;
  var step=updateProgressValue<28?3.4:(updateProgressValue<65?1.7:.55);
  setUpdateProgress(Math.min(ceiling,updateProgressValue+step));
 },110);
}
function finishUpdateProgress(success){
 if(updateProgressTimer){window.clearInterval(updateProgressTimer);updateProgressTimer=0;}
 setUpdateProgress(success?100:0);
}
function installAppUpdate(){
 if(updateInstallBusy||!updateState||!updateState.CanAutoUpdate)return;
 updateInstallBusy=true;
 var modal=document.getElementById('updateModal');
 modal.className='update-modal show installing';
 document.getElementById('updateLaterButton').disabled=true;
 document.getElementById('updateReleaseButton').disabled=true;
 var button=document.getElementById('updateInstallButton');
 button.disabled=true;button.innerText='VERIFYING UPDATE...';
 var status=document.getElementById('updateStatus');
 status.className='update-status';
 status.innerText='Downloading the verified app and patch companion, then checking both SHA-256 digests. ScrapLab will restart when verification finishes.';
 startUpdateProgress();
 var started=false;
 try{
  started=!!window.external.InstallAppUpdate(
   String(updateState.AssetUrl),String(updateState.AssetDigest),
   String(updateState.PatchAssetUrl),String(updateState.PatchAssetDigest),
   String(updateState.LatestVersion));
 }catch(e){}
 if(!started)receiveUpdateInstall(JSON.stringify({Success:false,Error:'The update service is already busy.'}),false);
}
function receiveUpdateInstall(text){
 var data=parseResult(text);
 var modal=document.getElementById('updateModal'),status=document.getElementById('updateStatus');
 if(data.Success&&data.ReadyToRestart){
  finishUpdateProgress(true);
  status.className='update-status good';
  status.innerText='UPDATE VERIFIED. Closing ScrapLab and reopening the new version...';
  document.getElementById('updateInstallButton').innerText='RESTARTING...';
  window.setTimeout(function(){window.external.CloseWindow();},850);
  return;
 }
 finishUpdateProgress(false);
 updateInstallBusy=false;
 modal.className='update-modal show';
 status.className='update-status bad';
 status.innerText=data.Error||'The update could not be installed. This copy was not changed.';
 document.getElementById('updateLaterButton').disabled=false;
 document.getElementById('updateReleaseButton').disabled=false;
 var button=document.getElementById('updateInstallButton');
 button.disabled=!updateState||!updateState.CanAutoUpdate;
 button.innerText='TRY UPDATE AGAIN';
}
function showUpdateToast(title,message,kind,duration){
 var toast=document.getElementById('updateToast');
 if(!toast)return;
 if(updateToastTimer)window.clearTimeout(updateToastTimer);
 document.getElementById('updateToastTitle').innerText=title||'UPDATE STATUS';
 document.getElementById('updateToastText').innerText=message||'';
 toast.className='update-toast show'+(kind?' '+kind:'');
 updateToastTimer=window.setTimeout(closeUpdateToast,duration||5500);
}
function closeUpdateToast(){
 if(updateToastTimer)window.clearTimeout(updateToastTimer);
 updateToastTimer=0;
 var toast=document.getElementById('updateToast');
 if(toast)toast.className='update-toast';
}
function secretTriggerMouseDown(e){
 e=e||window.event;
 e.cancelBubble=true;
 if(e.preventDefault)e.preventDefault();
 e.returnValue=false;
 return false;
}
function captureSecretCompatibility(kind,data){
 var state=String(data.CompatibilityState||'');
 var canApply=data.CanApply!==false;
 var reason=String(data.CompatibilityReason||'');
 if(kind==='resource'){
 secretResourceLocatorCompatibility=state;
 secretResourceLocatorCanApply=canApply;
 secretResourceLocatorReason=reason;
 }else if(kind==='revival'){
  secretRevivalBuffCompatibility=state;
  secretRevivalBuffCanApply=canApply;
  secretRevivalBuffReason=reason;
 }else if(kind==='commands'){
  secretDeveloperCommandsCompatibility=state;
  secretDeveloperCommandsCanApply=canApply;
  secretDeveloperCommandsReason=reason;
 }else if(kind==='chemical'){
  secretChemicalFertilizerCompatibility=state;
  secretChemicalFertilizerCanApply=canApply;
  secretChemicalFertilizerReason=reason;
 }else if(kind==='cannon'){
  secretDualFluidCannonCompatibility=state;
  secretDualFluidCannonCanApply=canApply;
  secretDualFluidCannonReason=reason;
 }
}
function compatibilityStateLabel(installed,state,fallback){
 if(installed)return 'INSTALLED';
 if(state==='COMPATIBLE GAME UPDATE')return 'GAME UPDATED - RE-ENABLE';
 if(state==='GAME UPDATE CHANGED REQUIRED CODE')return 'GAME UPDATE CHANGED REQUIRED CODE';
 if(state==='OTHER MODIFICATION DETECTED')return 'OTHER MODIFICATION DETECTED';
 if(state==='PARTIAL PATCH - REPAIR REQUIRED')return 'PARTIAL PATCH \u2014 REPAIR REQUIRED';
 return fallback||'NOT INSTALLED';
}
function renderCompatibilityReason(id,installed,canApply,reason){
 var node=document.getElementById(id);
 if(!node)return;
 var show=!installed&&!canApply&&!!reason;
 node.className=show?'secret-compat-reason show':'secret-compat-reason';
 node.innerText=show?reason:'';
}
function loadSecretModsState(){
 try{secretModsEnabled=!!window.external.GetSecretModsEnabled();}catch(e){secretModsEnabled=false;}
 var installedSecretMod=false;
 try{
  var data=parseResult(window.external.GetResourceLocatorModStatus());
  if(data.Success){
   secretResourceLocatorInstalled=!!data.Installed;
   secretResourceLocatorNeedsUpdate=!!data.NeedsUpdate;
   captureSecretCompatibility('resource',data);
   installedSecretMod=installedSecretMod||secretResourceLocatorInstalled;
  }else{
   showSecretModFeedback(data.Error||'Could not read the installed resource-locator state.','bad');
  }
 }catch(e){
 showSecretModFeedback('Could not read the installed resource-locator state.','bad');
 }
 try{
  var revivalData=parseResult(window.external.GetRevivalBuffModStatus());
  if(revivalData.Success){
   secretRevivalBuffInstalled=!!revivalData.Installed;
   captureSecretCompatibility('revival',revivalData);
   installedSecretMod=installedSecretMod||secretRevivalBuffInstalled;
  }else{
   showSecretModFeedback(revivalData.Error||'Could not read the installed revival-buff state.','bad');
  }
 }catch(e){
  showSecretModFeedback('Could not read the installed revival-buff state.','bad');
 }
 try{
  var commandData=parseResult(window.external.GetDeveloperCommandsModStatus());
  if(commandData.Success){
   secretDeveloperCommandsInstalled=!!commandData.Installed;
   secretDeveloperCommandsMode=commandData.Mode==='everyone'?'everyone':'host';
   secretDeveloperCommandsError='';
   captureSecretCompatibility('commands',commandData);
   installedSecretMod=installedSecretMod||secretDeveloperCommandsInstalled;
  }else{
   secretDeveloperCommandsError=commandData.Error||'Unsupported SurvivalGame script state.';
   showSecretModFeedback(commandData.Error||'Could not read the host developer-command state.','bad');
  }
 }catch(e){
  secretDeveloperCommandsError='Could not read the SurvivalGame script state.';
  showSecretModFeedback('Could not read the host developer-command state.','bad');
 }
 try{
  var chemicalData=parseResult(window.external.GetChemicalFertilizerModStatus());
  if(chemicalData.Success){
   secretChemicalFertilizerInstalled=!!chemicalData.Installed;
   captureSecretCompatibility('chemical',chemicalData);
   installedSecretMod=installedSecretMod||secretChemicalFertilizerInstalled;
  }else{
   showSecretModFeedback(chemicalData.Error||'Could not read the installed chemical-fertilizer state.','bad');
  }
 }catch(e){
  showSecretModFeedback('Could not read the installed chemical-fertilizer state.','bad');
 }
 try{
  var cannonData=parseResult(window.external.GetDualFluidCannonModStatus());
  if(cannonData.Success){
   secretDualFluidCannonInstalled=!!cannonData.Installed;
   secretDualFluidCannonError='';
   captureSecretCompatibility('cannon',cannonData);
   installedSecretMod=installedSecretMod||secretDualFluidCannonInstalled;
  }else{
   secretDualFluidCannonError=cannonData.Error||'Unsupported cannon script state.';
   showSecretModFeedback(cannonData.Error||'Could not read the installed dual-fluid cannon state.','bad');
  }
 }catch(e){
  secretDualFluidCannonError='Could not read the cannon script state.';
  showSecretModFeedback('Could not read the installed dual-fluid cannon state.','bad');
 }
 if(installedSecretMod&&!secretModsEnabled){
  secretModsEnabled=true;
  try{window.external.SetSecretModsEnabled(true);}catch(ignore){}
 }
 renderSecretModsState();
}
function renderSecretModsState(){
 var control=document.getElementById('secretModsMaster');
 var row=document.getElementById('secretModsMasterRow');
 var status=document.getElementById('secretModsStatus');
 if(!control||!row||!status)return;
 control.className=secretModsEnabled?'secret-switch on':'secret-switch';
 control.setAttribute('aria-checked',secretModsEnabled?'true':'false');
 control.disabled=!!operationBusy||!!secretModBusy;
 row.className=secretModsEnabled?'secret-mod-row enabled':'secret-mod-row';
 status.className=secretModsEnabled?'secret-mods-status on':'secret-mods-status';
 status.getElementsByTagName('span')[0].innerText=secretModsEnabled?'SECRET PATCH SYSTEM ARMED':'SECRET PATCH SYSTEM OFFLINE';
 var locator=document.getElementById('resourceLocatorSwitch');
 var locatorRow=document.getElementById('resourceLocatorRow');
 var locatorState=document.getElementById('resourceLocatorState');
 if(locator&&locatorRow&&locatorState){
  locator.className=secretResourceLocatorInstalled?'secret-switch on':'secret-switch';
  locator.setAttribute('aria-checked',secretResourceLocatorInstalled?'true':'false');
  locator.disabled=operationBusy||!secretModsEnabled||secretModBusy||!!gameRunning||!secretResourceLocatorCanApply;
  locatorRow.className='secret-mod-row secret-mod-card'+(secretResourceLocatorInstalled?' enabled':'')+((!secretModsEnabled||!secretResourceLocatorCanApply)?' locked':'');
  locatorState.innerText=secretModBusy&&secretModBusyTarget==='resource'?'APPLYING...':(gameRunning?'GAME RUNNING · CLOSE IT FIRST':(secretResourceLocatorNeedsUpdate&&secretResourceLocatorCompatibility!=='COMPATIBLE GAME UPDATE'?'UPDATE READY · DOT VISIBILITY FIX':compatibilityStateLabel(secretResourceLocatorInstalled,secretResourceLocatorCompatibility,'NOT INSTALLED')));
  renderCompatibilityReason('resourceLocatorReason',secretResourceLocatorInstalled,secretResourceLocatorCanApply,secretResourceLocatorReason);
 }
 var revival=document.getElementById('revivalBuffSwitch');
 var revivalRow=document.getElementById('revivalBuffRow');
 var revivalState=document.getElementById('revivalBuffState');
 if(revival&&revivalRow&&revivalState){
  revival.className=secretRevivalBuffInstalled?'secret-switch on':'secret-switch';
  revival.setAttribute('aria-checked',secretRevivalBuffInstalled?'true':'false');
  revival.disabled=operationBusy||!secretModsEnabled||secretModBusy||!!gameRunning||!secretRevivalBuffCanApply;
  revivalRow.className='secret-mod-row secret-mod-card'+(secretRevivalBuffInstalled?' enabled':'')+((!secretModsEnabled||!secretRevivalBuffCanApply)?' locked':'');
  revivalState.innerText=secretModBusy&&secretModBusyTarget==='revival'?'APPLYING...':(gameRunning?'GAME RUNNING - CLOSE IT FIRST':compatibilityStateLabel(secretRevivalBuffInstalled,secretRevivalBuffCompatibility,'NOT INSTALLED'));
  renderCompatibilityReason('revivalBuffReason',secretRevivalBuffInstalled,secretRevivalBuffCanApply,secretRevivalBuffReason);
 }
 var commands=document.getElementById('developerCommandsSwitch');
 var commandOptions=document.getElementById('developerCommandsOptions');
 var commandsRow=document.getElementById('developerCommandsRow');
 var commandsState=document.getElementById('developerCommandsState');
 if(commands&&commandsRow&&commandsState){
  commands.className=secretDeveloperCommandsInstalled?'secret-switch on':'secret-switch';
  commands.setAttribute('aria-checked',secretDeveloperCommandsInstalled?'true':'false');
  commands.disabled=operationBusy||!secretModsEnabled||secretModBusy||!!gameRunning||!!secretDeveloperCommandsError||!secretDeveloperCommandsCanApply;
  if(commandOptions)commandOptions.disabled=operationBusy||!secretModsEnabled||secretModBusy||!!gameRunning||!!secretDeveloperCommandsError||!secretDeveloperCommandsCanApply;
  commandsRow.className='secret-mod-row secret-mod-card'+(secretDeveloperCommandsInstalled?' enabled':'')+((!secretModsEnabled||secretDeveloperCommandsError||!secretDeveloperCommandsCanApply)?' locked':'');
  commandsState.innerText=secretModBusy&&secretModBusyTarget==='commands'?'APPLYING...':(gameRunning?'GAME RUNNING - CLOSE IT FIRST':(secretDeveloperCommandsError?'UNSUPPORTED FILE - NO CHANGES':compatibilityStateLabel(secretDeveloperCommandsInstalled,secretDeveloperCommandsCompatibility,'NOT INSTALLED')));
  renderCompatibilityReason('developerCommandsReason',secretDeveloperCommandsInstalled,secretDeveloperCommandsCanApply,secretDeveloperCommandsReason);
 }
 var chemical=document.getElementById('chemicalFertilizerSwitch');
 var chemicalRow=document.getElementById('chemicalFertilizerRow');
 var chemicalState=document.getElementById('chemicalFertilizerState');
 if(chemical&&chemicalRow&&chemicalState){
  chemical.className=secretChemicalFertilizerInstalled?'secret-switch on':'secret-switch';
  chemical.setAttribute('aria-checked',secretChemicalFertilizerInstalled?'true':'false');
  chemical.disabled=operationBusy||!secretModsEnabled||secretModBusy||!!gameRunning||!secretChemicalFertilizerCanApply;
  chemicalRow.className='secret-mod-row secret-mod-card'+(secretChemicalFertilizerInstalled?' enabled':'')+((!secretModsEnabled||!secretChemicalFertilizerCanApply)?' locked':'');
  chemicalState.innerText=secretModBusy&&secretModBusyTarget==='chemical'?'APPLYING...':(gameRunning?'GAME RUNNING - CLOSE IT FIRST':compatibilityStateLabel(secretChemicalFertilizerInstalled,secretChemicalFertilizerCompatibility,'NOT INSTALLED'));
  renderCompatibilityReason('chemicalFertilizerReason',secretChemicalFertilizerInstalled,secretChemicalFertilizerCanApply,secretChemicalFertilizerReason);
 }
 var cannon=document.getElementById('dualFluidCannonSwitch');
 var cannonRow=document.getElementById('dualFluidCannonRow');
 var cannonState=document.getElementById('dualFluidCannonState');
 if(cannon&&cannonRow&&cannonState){
  cannon.className=secretDualFluidCannonInstalled?'secret-switch on':'secret-switch';
  cannon.setAttribute('aria-checked',secretDualFluidCannonInstalled?'true':'false');
  cannon.disabled=operationBusy||!secretModsEnabled||secretModBusy||!!gameRunning||!!secretDualFluidCannonError||!secretDualFluidCannonCanApply;
  cannonRow.className='secret-mod-row secret-mod-card'+(secretDualFluidCannonInstalled?' enabled':'')+((!secretModsEnabled||secretDualFluidCannonError||!secretDualFluidCannonCanApply)?' locked':'');
  cannonState.innerText=secretModBusy&&secretModBusyTarget==='cannon'?'APPLYING...':(gameRunning?'GAME RUNNING - CLOSE IT FIRST':(secretDualFluidCannonError?'UNSUPPORTED FILE - NO CHANGES':(secretDualFluidCannonInstalled?(secretChemicalFertilizerInstalled?'INSTALLED':'DEPENDENCY MISSING - REPAIR REQUIRED'):compatibilityStateLabel(false,secretDualFluidCannonCompatibility,(secretChemicalFertilizerInstalled?'NOT INSTALLED':'READY - INSTALLS FERTILIZER')))));
  renderCompatibilityReason('dualFluidCannonReason',secretDualFluidCannonInstalled,secretDualFluidCannonCanApply,secretDualFluidCannonReason);
 }
 var count=document.getElementById('secretModCount');
 if(count){
  var active=(secretResourceLocatorInstalled?1:0)+(secretRevivalBuffInstalled?1:0)+(secretDeveloperCommandsInstalled?1:0)+(secretChemicalFertilizerInstalled?1:0)+(secretDualFluidCannonInstalled?1:0);
  count.innerText=active+' ACTIVE \u00b7 5 AVAILABLE';
 }
 filterSecretMods();
}
function filterSecretMods(){
 var input=document.getElementById('secretModSearch');
 var list=document.getElementById('secretModsList');
 var empty=document.getElementById('secretModsEmpty');
 if(!list||!empty)return;
 var query=input?String(input.value||'').toLowerCase().replace(/^\s+|\s+$/g,''):'';
 var rows=list.getElementsByTagName('div');
 var shown=0;
 for(var i=0;i<rows.length;i++){
  var row=rows[i];
  if((' '+row.className+' ').indexOf(' secret-mod-card ')<0)continue;
  var haystack=String(row.getAttribute('data-search')||'').toLowerCase();
  var visible=!query||haystack.indexOf(query)>=0;
  row.style.display=visible?'flex':'none';
  if(visible)shown++;
 }
 empty.style.display=shown?'none':'block';
}
function showSecretModFeedback(message,type){
 var feedback=document.getElementById('secretModsFeedback');
 if(!feedback)return;
 feedback.className=message?'secret-mods-feedback '+(type||'show'):'secret-mods-feedback';
 feedback.innerText=message||'';
}
function toggleSecretMods(e){
 e=e||window.event;
 e.cancelBubble=true;
 if(operationBusy||tutorialActive)return false;
 var layer=document.getElementById('secretModsLayer');
 if(layer.className.indexOf(' show')>=0){closeSecretMods();return false;}
 closeSaveMenu();closeHotfixConfirm();
 document.getElementById('secretModsTrigger').className='window-emblem secret-trigger armed';
 layer.className='secret-mods-layer show';
 return false;
}
function closeSecretMods(){
 document.getElementById('secretModsLayer').className='secret-mods-layer';
 document.getElementById('secretModsTrigger').className='window-emblem secret-trigger';
}
function secretModsBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('secretModsLayer'))closeSecretMods();
}
function toggleSecretModsEnabled(){
 if(operationBusy||secretModBusy)return;
 if(secretModsEnabled&&secretDualFluidCannonInstalled){openCannonDangerConfirm('masterOff');return;}
 if(secretModsEnabled){disableAllSecretModsConfirmed();return;}
 secretModsEnabled=!secretModsEnabled;
 try{window.external.SetSecretModsEnabled(secretModsEnabled);}catch(e){}
 showSecretModFeedback(
  secretModsEnabled
   ?(gameRunning?'PATCH BAY ARMED — close Scrap Mechanic before changing secret mods.':'PATCH BAY ARMED — choose an individual secret mod to install it.')
   :'PATCH BAY OFFLINE — all installed secret mods are disabled.',
  secretModsEnabled?'good':'show');
 renderSecretModsState();
}
function disableAllSecretModsConfirmed(){
 if(secretDualFluidCannonInstalled&&secretChemicalFertilizerInstalled){
  if(!setChemicalFertilizerMod(false))return false;
 }else{
  if(secretDualFluidCannonInstalled&&!setDualFluidCannonMod(false))return false;
  if(secretChemicalFertilizerInstalled&&!setChemicalFertilizerMod(false))return false;
 }
 if(secretRevivalBuffInstalled&&!setRevivalBuffMod(false))return false;
 if(secretDeveloperCommandsInstalled&&!setDeveloperCommandsMod(false))return false;
 if(secretResourceLocatorInstalled&&!setResourceLocatorMod(false))return false;
 secretModsEnabled=false;
 try{window.external.SetSecretModsEnabled(false);}catch(e){}
 showSecretModFeedback('PATCH BAY OFFLINE - all installed secret mods are disabled.','show');
 renderSecretModsState();
 return true;
}
function toggleResourceLocatorMod(){
 if(operationBusy||!secretModsEnabled||secretModBusy)return;
 setResourceLocatorMod(secretResourceLocatorNeedsUpdate||!secretResourceLocatorInstalled);
}
function toggleRevivalBuffMod(){
 if(operationBusy||!secretModsEnabled||secretModBusy)return;
 setRevivalBuffMod(!secretRevivalBuffInstalled);
}
function setRevivalBuffMod(enabled){
 if(gameRunning){
  showSecretModFeedback('Close Scrap Mechanic completely before changing Revival Buff Recovery.','bad');
  return false;
 }
 secretModBusy=true;secretModBusyTarget='revival';operationBusy=true;
 showSecretModFeedback(
  enabled?'PREPARING REVIVAL BUFF RECOVERY...':'REMOVING REVIVAL BUFF RECOVERY...',
  'working');
 renderSecretModsState();applyGameLock(gameRunning);
 var data;
 try{data=parseResult(window.external.SetRevivalBuffMod(enabled));}
 catch(e){data={Success:false,Error:e.message||'The revival-buff installer did not return a result.'};}
 secretModBusy=false;secretModBusyTarget='';operationBusy=false;
 if(data.Cancelled){
  showSecretModFeedback('No changes were made because administrator permission was cancelled.','show');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 if(!data.Success){
  showSecretModFeedback(data.Error||'Revival Buff Recovery could not be changed.','bad');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 secretRevivalBuffInstalled=!!data.Installed;
 if(data.BackupPath)lastGameBackupPath=data.BackupPath;
 loadSecretModsState();
 showSecretModFeedback(
  secretRevivalBuffInstalled
   ?'REVIVAL BUFF RECOVERY INSTALLED - Revival Baguettes now restore all pizza and veggie-burger buffs held at knockout.'
   :'REVIVAL BUFF RECOVERY REMOVED - normal SurvivalPlayer behavior was restored.',
  'good');
 applyGameLock(gameRunning);renderSecretModsState();
 return true;
}
function setResourceLocatorMod(enabled){
 if(gameRunning){
  showSecretModFeedback('Close Scrap Mechanic completely before changing Resource Locator Dots.','bad');
  return false;
 }
 secretModBusy=true;secretModBusyTarget='resource';operationBusy=true;
 showSecretModFeedback(
  enabled?'PREPARING RESOURCE LOCATOR DOTS INSTALLATION...':'PREPARING RESOURCE LOCATOR DOTS REMOVAL...',
  'working');
 renderSecretModsState();applyGameLock(gameRunning);
 var data;
 try{data=parseResult(window.external.SetResourceLocatorMod(enabled));}
 catch(e){data={Success:false,Error:e.message||'The secret-mod installer did not return a result.'};}
 secretModBusy=false;secretModBusyTarget='';operationBusy=false;
 if(data.Cancelled){
  showSecretModFeedback('No changes were made because administrator permission was cancelled.','show');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 if(!data.Success){
  showSecretModFeedback(data.Error||'Resource Locator Dots could not be changed.','bad');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 secretResourceLocatorInstalled=!!data.Installed;
 secretResourceLocatorNeedsUpdate=!!data.NeedsUpdate;
 if(data.BackupPath)lastGameBackupPath=data.BackupPath;
 loadSecretModsState();
 showSecretModFeedback(
  secretResourceLocatorInstalled
   ?'RESOURCE LOCATOR DOTS INSTALLED — equip the Connect Tool to reveal nearby resource cores. The locator output stays inactive.'
   :'RESOURCE LOCATOR DOTS REMOVED — the verified original HarvestCore script was restored.',
  'good');
 applyGameLock(gameRunning);renderSecretModsState();
 return true;
}
function toggleDeveloperCommandsMod(){
 if(operationBusy||!secretModsEnabled||secretModBusy)return;
 if(secretDeveloperCommandsInstalled){setDeveloperCommandsMod(false);return;}
 openDeveloperCommandOptions();
}
function setDeveloperCommandsMod(enabled,mode){
 if(gameRunning){
  showSecretModFeedback('Close Scrap Mechanic completely before changing Developer Commands.','bad');
  return false;
 }
 var selectedMode=mode==='everyone'?'everyone':'host';
 secretModBusy=true;secretModBusyTarget='commands';operationBusy=true;
 showSecretModFeedback(
  enabled?'PREPARING DEVELOPER COMMAND ACCESS...':'PREPARING DEVELOPER COMMANDS REMOVAL...',
  'working');
 renderSecretModsState();applyGameLock(gameRunning);
 var data;
 try{data=parseResult(window.external.SetDeveloperCommandsMod(enabled,selectedMode));}
 catch(e){data={Success:false,Error:e.message||'The developer-command installer did not return a result.'};}
 secretModBusy=false;secretModBusyTarget='';operationBusy=false;
 if(data.Cancelled){
  showSecretModFeedback('No changes were made because administrator permission was cancelled.','show');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 if(!data.Success){
  showSecretModFeedback(data.Error||'Developer Commands could not be changed.','bad');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 secretDeveloperCommandsInstalled=!!data.Installed;
 secretDeveloperCommandsMode=data.Mode==='everyone'?'everyone':selectedMode;
 secretDeveloperCommandsError='';
 if(data.BackupPath)lastGameBackupPath=data.BackupPath;
 loadSecretModsState();
 showSecretModFeedback(
  secretDeveloperCommandsInstalled
   ?(secretDeveloperCommandsMode==='everyone'
     ?'DEVELOPER COMMANDS READY FOR EVERY PLAYER - all joined players receive the built-in command list.'
     :'DEVELOPER COMMANDS READY FOR HOST ONLY - open chat and enter a built-in command such as /unlimited.')
   :'DEVELOPER COMMANDS REMOVED - the verified original SurvivalGame script was restored.',
  'good');
 applyGameLock(gameRunning);renderSecretModsState();
 return true;
}
function openDeveloperCommandOptions(){
 if(gameRunning){
  showSecretModFeedback('Close Scrap Mechanic completely before changing Developer Command options.','bad');
  return;
 }
 developerCommandDraftMode=secretDeveloperCommandsMode||'host';
 document.getElementById('developerEveryoneAck').checked=false;
 renderDeveloperCommandOptions();
 document.getElementById('developerCommandModal').className='hotfix-modal developer-command-modal show';
 window.setTimeout(function(){document.getElementById(developerCommandDraftMode==='everyone'?'developerModeEveryone':'developerModeHost').focus();},30);
}
function closeDeveloperCommandConfirm(){
 document.getElementById('developerCommandModal').className='hotfix-modal developer-command-modal';
 document.getElementById('developerEveryoneAck').checked=false;
}
function developerCommandBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('developerCommandModal'))closeDeveloperCommandConfirm();
}
function selectDeveloperCommandMode(mode){
 developerCommandDraftMode=mode==='everyone'?'everyone':'host';
 document.getElementById('developerEveryoneAck').checked=false;
 renderDeveloperCommandOptions();
}
function renderDeveloperCommandOptions(){
 var everyone=developerCommandDraftMode==='everyone';
 document.getElementById('developerModeHost').className='command-access-option'+(!everyone?' selected':'');
 document.getElementById('developerModeEveryone').className='command-access-option'+(everyone?' selected':'');
 document.getElementById('developerEveryoneWarning').className=everyone?'command-everyone-warning show':'command-everyone-warning';
 var button=document.getElementById('developerCommandConfirmButton');
 var unchanged=secretDeveloperCommandsInstalled&&secretDeveloperCommandsMode===developerCommandDraftMode;
 button.innerHTML='<span>!</span>'+(secretDeveloperCommandsInstalled?'APPLY ACCESS MODE':(everyone?'INSTALL FOR EVERY PLAYER':'INSTALL HOST ONLY'));
 button.disabled=unchanged||(everyone&&!document.getElementById('developerEveryoneAck').checked);
 document.getElementById('developerCommandFootNote').innerText=unchanged
  ?'This access mode is already installed.'
  :(everyone?'Every joined player will receive command access while connected.':'Only the world host will receive command access.');
}
function updateDeveloperCommandOptionButton(){
 renderDeveloperCommandOptions();
}
function applyDeveloperCommandOptions(){
 if(document.getElementById('developerCommandConfirmButton').disabled)return;
 var mode=developerCommandDraftMode;
 closeDeveloperCommandConfirm();
 setDeveloperCommandsMod(true,mode);
}
function toggleChemicalFertilizerMod(){
 if(operationBusy||!secretModsEnabled||secretModBusy)return;
 if(secretChemicalFertilizerInstalled&&secretDualFluidCannonInstalled){
  openCannonDangerConfirm('removeBoth');
  return;
 }
 setChemicalFertilizerMod(!secretChemicalFertilizerInstalled);
}
function setChemicalFertilizerMod(enabled){
 if(gameRunning){
  showSecretModFeedback('Close Scrap Mechanic completely before changing Chemical Fertilizer Splash.','bad');
  return false;
 }
 secretModBusy=true;secretModBusyTarget='chemical';operationBusy=true;
 showSecretModFeedback(
  enabled?'PREPARING CHEMICAL FERTILIZER SPLASH INSTALLATION...':'PREPARING CHEMICAL FERTILIZER SPLASH REMOVAL...',
  'working');
 renderSecretModsState();applyGameLock(gameRunning);
 var data;
 try{data=parseResult(window.external.SetChemicalFertilizerMod(enabled));}
 catch(e){data={Success:false,Error:e.message||'The secret-mod installer did not return a result.'};}
 secretModBusy=false;secretModBusyTarget='';operationBusy=false;
 if(data.Cancelled){
  showSecretModFeedback('No changes were made because administrator permission was cancelled.','show');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 if(!data.Success){
  showSecretModFeedback(data.Error||'Chemical Fertilizer Splash could not be changed.','bad');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 secretChemicalFertilizerInstalled=!!data.Installed;
 if(!secretChemicalFertilizerInstalled)secretDualFluidCannonInstalled=false;
 if(data.BackupPath)lastGameBackupPath=data.BackupPath;
 loadSecretModsState();
 showSecretModFeedback(
  secretChemicalFertilizerInstalled
   ?'CHEMICAL FERTILIZER SPLASH INSTALLED - chemical hits fertilize directly; red Farmbot pesticide fertilizes a 2.5-block radius.'
   :'CHEMICAL FERTILIZER SPLASH REMOVED - verified prior scripts were restored without removing other ScrapLab fixes.',
  'good');
 applyGameLock(gameRunning);renderSecretModsState();
 return true;
}
function toggleDualFluidCannonMod(){
 if(operationBusy||!secretModsEnabled||secretModBusy)return;
 if(!secretDualFluidCannonInstalled&&!secretChemicalFertilizerInstalled){
  openDependencyConfirm('installBoth');
  return;
 }
 if(secretDualFluidCannonInstalled){openCannonDangerConfirm('cannonOnly');return;}
 setDualFluidCannonMod(true);
}
function setDualFluidCannonMod(enabled){
 if(gameRunning){
  showSecretModFeedback('Close Scrap Mechanic completely before changing Dual-Fluid Water Cannon.','bad');
  return false;
 }
 secretModBusy=true;secretModBusyTarget='cannon';operationBusy=true;
 showSecretModFeedback(
  enabled?'PREPARING DUAL-FLUID WATER CANNON INSTALLATION...':'PREPARING DUAL-FLUID WATER CANNON REMOVAL...',
  'working');
 renderSecretModsState();applyGameLock(gameRunning);
 var data;
 try{data=parseResult(window.external.SetDualFluidCannonMod(enabled));}
 catch(e){data={Success:false,Error:e.message||'The dual-fluid installer did not return a result.'};}
 secretModBusy=false;secretModBusyTarget='';operationBusy=false;
 if(data.Cancelled){
  showSecretModFeedback('No changes were made because administrator permission was cancelled.','show');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 if(!data.Success){
  showSecretModFeedback(data.Error||'Dual-Fluid Water Cannon could not be changed.','bad');
  applyGameLock(gameRunning);renderSecretModsState();return false;
 }
 secretDualFluidCannonInstalled=!!data.Installed;
 secretDualFluidCannonError='';
 if(secretDualFluidCannonInstalled)secretChemicalFertilizerInstalled=true;
 if(data.BackupPath)lastGameBackupPath=data.BackupPath;
 loadSecretModsState();
 showSecretModFeedback(
  secretDualFluidCannonInstalled
   ?'DUAL-FLUID WATER CANNON INSTALLED - connect logic, water, and chemical; each pulse fires every available liquid.'
   :'DUAL-FLUID WATER CANNON REMOVED - Chemical Fertilizer Splash remains installed.',
  'good');
 applyGameLock(gameRunning);renderSecretModsState();
 return true;
}
function openDependencyConfirm(action){
 if(gameRunning){
  showSecretModFeedback('Close Scrap Mechanic completely before changing linked secret mods.','bad');
  return;
 }
 secretDependencyAction=action;
 var install=action==='installBoth';
 document.getElementById('dependencyTitle').innerText=install?'INSTALL REQUIRED DEPENDENCY':'REMOVE LINKED DEPENDENCY';
 document.getElementById('dependencyKicker').innerText=install?'DUAL-FLUID WATER CANNON STARTUP':'CHEMICAL FERTILIZER SPLASH SHUTDOWN';
 document.getElementById('dependencyIntro').innerText=install
  ?'Dual-Fluid Water Cannon requires Chemical Fertilizer Splash. ScrapLab will install both in one protected operation.'
  :'Dual-Fluid Water Cannon depends on Chemical Fertilizer Splash. ScrapLab must remove the cannon patch first.';
 document.getElementById('dependencyFirstChange').innerText=install
  ?'Install Chemical Fertilizer Splash and verify all four game scripts.'
  :'Remove Dual-Fluid Water Cannon and restore the original cannon script.';
 document.getElementById('dependencySecondChange').innerText=install
  ?'Install Dual-Fluid Water Cannon after its dependency is verified.'
  :'Remove Chemical Fertilizer Splash only after the cannon is safely restored.';
 var button=document.getElementById('dependencyConfirmButton');
 button.innerHTML='<span>!</span>'+(install?'INSTALL FERTILIZER + DUAL-FLUID CANNON':'REMOVE CANNON + FERTILIZER');
 document.getElementById('dependencyModal').className='hotfix-modal dependency-modal show';
 window.setTimeout(function(){button.focus();},30);
}
function closeDependencyConfirm(){
 document.getElementById('dependencyModal').className='hotfix-modal dependency-modal';
 secretDependencyAction='';
}
function dependencyBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('dependencyModal'))closeDependencyConfirm();
}
function confirmDependencyChange(){
 var action=secretDependencyAction;
 closeDependencyConfirm();
 if(action==='installBoth')setDualFluidCannonMod(true);
 else if(action==='removeBoth')openCannonDangerConfirm('removeBoth');
}
function openCannonDangerConfirm(action){
 if(gameRunning){
  showSecretModFeedback('Close Scrap Mechanic completely before removing Dual-Fluid Water Cannon.','bad');
  return;
 }
 secretCannonDangerAction=action;
 var button=document.getElementById('cannonDangerConfirmButton');
 var ack=document.getElementById('cannonDangerAck');
 ack.checked=false;
 button.disabled=true;
 button.innerHTML='<span>!</span>'+(action==='removeBoth'?'REMOVE CANNON + FERTILIZER':(action==='masterOff'?'DISABLE ALL SECRET MODS':'DISABLE CANNON MOD'));
 document.getElementById('cannonDangerModal').className='hotfix-modal cannon-danger-modal show';
 window.setTimeout(function(){document.getElementById('cannonDangerCancel').focus();},30);
}
function updateCannonDangerConfirm(){
 var ack=document.getElementById('cannonDangerAck');
 document.getElementById('cannonDangerConfirmButton').disabled=!ack.checked;
}
function closeCannonDangerConfirm(){
 document.getElementById('cannonDangerModal').className='hotfix-modal cannon-danger-modal';
 document.getElementById('cannonDangerAck').checked=false;
 secretCannonDangerAction='';
}
function cannonDangerBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('cannonDangerModal'))closeCannonDangerConfirm();
}
function confirmCannonDangerChange(){
 if(!document.getElementById('cannonDangerAck').checked)return;
 var action=secretCannonDangerAction;
 closeCannonDangerConfirm();
 if(action==='removeBoth')setChemicalFertilizerMod(false);
 else if(action==='masterOff')disableAllSecretModsConfirmed();
 else if(action==='cannonOnly')setDualFluidCannonMod(false);
}
function completeTutorialPrompt(){
 try{window.external.CompleteTutorialPrompt();}catch(e){}
}
function showTutorialPrompt(){
 if(operationBusy||tutorialActive)return;
 closeSaveMenu();closeSecretMods();
 document.getElementById('onboardModal').className='onboard-modal show';
 window.setTimeout(function(){var button=document.getElementById('onboardStart');if(button)button.focus();},30);
}
function checkFirstRunTutorial(){
 if(operationBusy){
  if(tutorialOfferChecks++<20)window.setTimeout(checkFirstRunTutorial,250);
  return;
 }
 try{if(window.external.ShouldOfferTutorial())showTutorialPrompt();}catch(e){}
}
function acceptTutorial(){
 completeTutorialPrompt();
 document.getElementById('onboardModal').className='onboard-modal';
 startTutorial();
}
function declineTutorial(){
 completeTutorialPrompt();
 document.getElementById('onboardModal').className='onboard-modal';
 window.setTimeout(maybeShowPendingUpdate,180);
}
function openHelp(){
 if(operationBusy||tutorialActive)return;
 closeSaveMenu();closeHotfixConfirm();closeSecretMods();
 document.getElementById('helpStatus').className='help-status';
 document.getElementById('helpStatus').innerText='Tutorial controls are available here whenever you need them.';
 document.getElementById('helpModal').className='help-modal show';
}
function closeHelp(){
 stopSmoothScroll(document.getElementById('helpBody'));
 document.getElementById('helpModal').className='help-modal';
 window.setTimeout(maybeShowPendingUpdate,180);
}
function helpBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('helpModal'))closeHelp();
}
function replayTutorial(){closeHelp();startTutorial();}
function resetTutorialPrompt(){
 try{window.external.ResetTutorialPrompt();}catch(e){}
 var status=document.getElementById('helpStatus');
 status.className='help-status good';
 status.innerText='FIRST-RUN PROMPT RESET — it will be offered the next time ScrapLab starts.';
}
function startTutorial(){
 if(operationBusy)return;
 closeHelp();closeHotfixConfirm();closeSecretMods();
 document.getElementById('onboardModal').className='onboard-modal';
 tutorialActive=true;tutorialIndex=0;
 var tutorialPane=document.getElementById('appScroll');
 stopSmoothScroll(tutorialPane);
 tutorialStartScroll=tutorialPane.scrollTop;
 tutorialResultHtml=document.getElementById('result').innerHTML;
 renderTutorialDiagnostics();
 document.getElementById('tutorial').className='tutorial show';
 showTutorialStep();
}
function renderTutorialDiagnostics(){
 var example={
  Success:true,DatabaseStatus:'ok',SaveVersion:28,GameTick:'12,458,920',RaidCount:1,Size:'31.1 MB',CanClear:false,
  OrphanedRaidCropCount:0,UnreadableRaidCropCount:0,UnreleasableRaidCropCount:0,CanRepairOrphanedCrops:false,
  DroppedItemsScanned:true,DroppedItemCount:1,DroppedItemQuantity:3,ExpiredDroppedItemCount:0,CanClearDroppedItems:false,CanClearExpiredDroppedItems:false,UnreadableDroppedItemCount:0,DroppedItemIcons:{},
  Warnings:['TUTORIAL EXAMPLE — sample raid data only. No save has been opened or changed.'],
  Raids:[{
   Number:1,Tier:4,Key:'EXAMPLE RAID RECORD · SAMPLE DATA',ThreatValue:824,MaximumThreatValue:1000,
   State:'WAVE STORED',PlannedEnemyCount:7,SpawnGroups:2,WorldSlot:11,WorldName:'Warehouse 2 - Floor 3',Center:{X:873,Y:107,Z:47},
   TrackedCrops:53,StaleCropReferences:53,LiveRaiderReferences:0,TickCounter:4872,TimeoutTick:4890,
   Enemies:[{Name:'Haybot',Quantity:4},{Name:'Totebot',Quantity:2},{Name:'Tapebot',Quantity:1}],
   Crops:[{Name:'Tomato',Quantity:31},{Name:'Broccoli',Quantity:22}],
   SavedTick:12454102,LastSpawnTick:12449230,NeedsSpawnPoints:false,PlantingRecords:53,LooksStuck:true,
   Notes:['This example shows the kind of raid information ScrapLab reads from a real world.']
  }],
  DroppedItems:[{
   EntityId:4223,WorldId:1,WorldName:'Overworld',CellX:-37,CellY:-42,Uuid:'db66f0b1-0c50-4b74-bdc7-771374204b1f',
   Name:'Big Wheel',Description:'A large wheel dropped from an inventory.',Quantity:3,ValueScore:420,ValueTier:'VALUABLE',
   DropType:'Loose pickup',Position:{X:-2624.4,Y:-2349.7,Z:8.0},KillTick:12601000,
   RemainingSeconds:3552,Expired:false,Epic:false,QuestItem:false
  }]
 };
 renderAnalysis(example);
 var result=document.getElementById('result');
 var raids=result.getElementsByClassName('raid');
 if(raids.length){
  var heads=raids[0].getElementsByClassName('raid-head');
  (heads.length?heads[0]:raids[0]).id='tutorialRaidExample';
 }
 var patchButton=document.getElementById('installHotfixBtn');
 if(patchButton){patchButton.disabled=true;patchButton.title='Disabled during the tutorial';}
}
function tutorialTarget(step){
 var target=document.getElementById(step.target);
 if(!target&&step.fallback)target=document.getElementById(step.fallback);
 return target||document.getElementById('identityPanel');
}
function showTutorialStep(){
 if(!tutorialActive)return;
 var step=tutorialSteps[tutorialIndex],target=tutorialTarget(step);
 var pane=document.getElementById('appScroll'),rect=target.getBoundingClientRect();
 var viewportHeight=window.innerHeight||document.documentElement.clientHeight;
 if(target!==document.getElementById('helpBtn')&&(rect.top<52||rect.bottom>viewportHeight-18)){
  var desired=pane.scrollTop+rect.top-82;
  pane.scrollTop=Math.max(0,Math.min(pane.scrollHeight-pane.clientHeight,desired));
  rect=target.getBoundingClientRect();
 }
 var numberText=step.badge;
 var numberNode=document.getElementById('tutorialNumber');
 if(typeof numberNode.textContent!=='undefined')numberNode.textContent=numberText;else numberNode.innerText=numberText;
 document.getElementById('tutorialLabel').innerText=step.label;
 document.getElementById('tutorialTitle').innerText=step.title;
 document.getElementById('tutorialText').innerText=step.text;
 document.getElementById('tutorialTip').innerText=step.tip;
 var progress='';
 for(var i=0;i<tutorialSteps.length;i++)progress+='<i class=""'+(i<tutorialIndex?'done':(i===tutorialIndex?'current':''))+'""></i>';
 document.getElementById('tutorialProgress').innerHTML=progress;
 document.getElementById('tutorialBack').disabled=tutorialIndex===0;
 document.getElementById('tutorialNext').innerText=tutorialIndex===tutorialSteps.length-1?'FINISH':'NEXT';
 positionTutorial(target);
 var card=document.getElementById('tutorialCard');
 card.className='tutorial-card';
 card.offsetWidth;
 card.className='tutorial-card enter';
 document.getElementById('tutorialNext').focus();
}
function positionTutorial(target){
 if(!tutorialActive)return;
 var viewportWidth=window.innerWidth||document.documentElement.clientWidth;
 var viewportHeight=window.innerHeight||document.documentElement.clientHeight;
 var rect=target.getBoundingClientRect(),pad=8;
 var titleBarTarget=target===document.getElementById('helpBtn');
 var minimumTop=titleBarTarget?0:43;
 var left=Math.max(8,rect.left-pad),top=Math.max(minimumTop,rect.top-pad);
 var right=Math.min(viewportWidth-8,rect.right+pad),bottom=Math.min(viewportHeight-8,rect.bottom+pad);
 if(right-left<34){left=Math.max(8,(rect.left+rect.right)/2-17);right=left+34;}
 if(bottom-top<34){top=Math.max(minimumTop,(rect.top+rect.bottom)/2-17);bottom=Math.min(viewportHeight-8,top+34);}
 var focus=document.getElementById('tutorialFocus');
 focus.style.left=Math.round(left)+'px';focus.style.top=Math.round(top)+'px';
 focus.style.width=Math.round(right-left)+'px';focus.style.height=Math.round(bottom-top)+'px';
 setTutorialShade('tutorialShadeTop',0,0,viewportWidth,top);
 setTutorialShade('tutorialShadeRight',right,top,Math.max(0,viewportWidth-right),Math.max(0,bottom-top));
 setTutorialShade('tutorialShadeBottom',0,bottom,viewportWidth,Math.max(0,viewportHeight-bottom));
 setTutorialShade('tutorialShadeLeft',0,top,left,Math.max(0,bottom-top));
 var card=document.getElementById('tutorialCard'),cardWidth=card.offsetWidth,cardHeight=card.offsetHeight;
 var gap=15,cardLeft,cardTop;
 if(viewportHeight-bottom>=cardHeight+gap){
  cardLeft=(left+right-cardWidth)/2;cardTop=bottom+gap;
 }else if(top>=cardHeight+gap+42){
  cardLeft=(left+right-cardWidth)/2;cardTop=top-cardHeight-gap;
 }else if(viewportWidth-right>=cardWidth+gap){
  cardLeft=right+gap;cardTop=(top+bottom-cardHeight)/2;
 }else if(left>=cardWidth+gap){
  cardLeft=left-cardWidth-gap;cardTop=(top+bottom-cardHeight)/2;
 }else{
  cardLeft=viewportWidth-cardWidth-18;cardTop=viewportHeight-cardHeight-18;
 }
 cardLeft=Math.max(14,Math.min(viewportWidth-cardWidth-14,cardLeft));
 cardTop=Math.max(48,Math.min(viewportHeight-cardHeight-14,cardTop));
 card.style.left=Math.round(cardLeft)+'px';card.style.top=Math.round(cardTop)+'px';
}
function setTutorialShade(id,left,top,width,height){
 var shade=document.getElementById(id);
 shade.style.left=Math.round(left)+'px';shade.style.top=Math.round(top)+'px';
 shade.style.width=Math.round(width)+'px';shade.style.height=Math.round(height)+'px';
}
function nextTutorialStep(){
 if(tutorialIndex>=tutorialSteps.length-1){finishTutorial();return;}
 tutorialIndex++;showTutorialStep();
}
function previousTutorialStep(){
 if(tutorialIndex<=0)return;
 tutorialIndex--;showTutorialStep();
}
function skipTutorial(){finishTutorial();}
function finishTutorial(){
 completeTutorialPrompt();
 tutorialActive=false;
 document.getElementById('tutorial').className='tutorial';
 document.getElementById('result').innerHTML=tutorialResultHtml;
 tutorialResultHtml='';
 document.getElementById('appScroll').scrollTop=tutorialStartScroll;
 updateScrollBar();
 pollGameProcess();
 window.setTimeout(maybeShowPendingUpdate,180);
}
function requestUiFrame(callback){
 var request=window.requestAnimationFrame||window.msRequestAnimationFrame;
 if(request)return request.call(window,callback);
 return window.setTimeout(callback,16);
}
function setScrollActive(active){
 var body=document.body,name=body.className||'',padded=' '+name+' ';
 if(active&&padded.indexOf(' scroll-active ')<0)body.className=(name+' scroll-active').replace(/^\s+|\s+$/g,'');
 if(!active&&padded.indexOf(' scroll-active ')>=0)body.className=padded.replace(' scroll-active ',' ').replace(/^\s+|\s+$/g,'');
}
function markScrollActive(){
 setScrollActive(true);
 if(scrollIdleTimer)window.clearTimeout(scrollIdleTimer);
 scrollIdleTimer=window.setTimeout(function(){setScrollActive(false);scrollIdleTimer=0;},140);
}
function scheduleScrollUi(){
 if(scrollUiQueued)return;
 scrollUiQueued=true;
 requestUiFrame(function(){
  scrollUiQueued=false;
  updateScrollBar();
  if(tutorialActive)positionTutorial(tutorialTarget(tutorialSteps[tutorialIndex]));
 });
}
function updateScrollBar(){
 var pane=document.getElementById('appScroll'),track=document.getElementById('scrollTrack'),thumb=document.getElementById('scrollThumb');
 if(!pane||!track||!thumb)return;
 var viewport=pane.clientHeight,total=pane.scrollHeight;
 var hazard=document.getElementById('mainHazard'),hazardClass=pane.scrollTop>12?'hazard paused':'hazard';
 if(hazard&&hazard.className!==hazardClass)hazard.className=hazardClass;
 if(total<=viewport+1){if(track.className!=='scroll-track')track.className='scroll-track';return;}
 if(track.className!=='scroll-track show')track.className='scroll-track show';
 var usable=track.clientHeight-4;
 var thumbHeight=Math.max(38,Math.floor(usable*viewport/total));
 var travel=usable-thumbHeight;
 var maxScroll=total-viewport;
 var top=2+(maxScroll>0?Math.round(travel*pane.scrollTop/maxScroll):0);
 var heightValue=thumbHeight+'px',topValue=top+'px';
 if(thumb.style.height!==heightValue)thumb.style.height=heightValue;
 if(thumb.style.top!==topValue)thumb.style.top=topValue;
}
function stopSmoothScroll(pane){
 smoothScrollRunning=false;
 if(pane){smoothScrollPane=pane;smoothScrollTarget=pane.scrollTop;}
}
function runSmoothScroll(pane){
 if(!smoothScrollRunning||smoothScrollPane!==pane)return;
 var distance=smoothScrollTarget-pane.scrollTop;
 if(Math.abs(distance)<.7){
  pane.scrollTop=Math.round(smoothScrollTarget);
 smoothScrollRunning=false;
  return;
 }
 markScrollActive();
 var movement=distance*.24;
 if(Math.abs(movement)<1)movement=distance<0?-1:1;
 pane.scrollTop=pane.scrollTop+movement;
 requestUiFrame(function(){runSmoothScroll(pane);});
}
function smoothWheelInput(pane,e,cancelBubble){
 e=e||window.event;
 var delta=typeof e.wheelDelta!=='undefined'?-e.wheelDelta:(e.detail||0)*40;
 if(!delta)return true;
 if(smoothScrollPane!==pane){
  smoothScrollRunning=false;
  smoothScrollPane=pane;
  smoothScrollTarget=pane.scrollTop;
 }
 var direction=delta<0?-1:1;
 var amount=Math.max(32,Math.min(180,Math.abs(delta)*.85))*direction;
 var maximum=Math.max(0,pane.scrollHeight-pane.clientHeight);
 if(!smoothScrollRunning)smoothScrollTarget=pane.scrollTop;
 smoothScrollTarget=Math.max(0,Math.min(maximum,smoothScrollTarget+amount));
 if(!smoothScrollRunning){
  smoothScrollRunning=true;
  requestUiFrame(function(){runSmoothScroll(pane);});
 }
 if(cancelBubble)e.cancelBubble=true;
 e.returnValue=false;if(e.preventDefault)e.preventDefault();return false;
}
function nestedScrollTarget(node,pane){
 while(node&&node!==pane){
  var className=' '+String(node.className||'')+' ';
  if(className.indexOf(' save-menu ')>=0)return true;
  node=node.parentNode;
 }
 return false;
}
function setupScrollBar(){
 var pane=document.getElementById('appScroll'),track=document.getElementById('scrollTrack'),thumb=document.getElementById('scrollThumb');
 smoothScrollPane=pane;smoothScrollTarget=pane.scrollTop;
 pane.onscroll=function(){
  if(!smoothScrollRunning)smoothScrollTarget=pane.scrollTop;
  markScrollActive();
  scheduleScrollUi();
 };
 pane.onmousewheel=function(e){
  e=e||window.event;
  if(nestedScrollTarget(e.target||e.srcElement,pane))return true;
  return smoothWheelInput(pane,e,false);
 };
 var helpBody=document.getElementById('helpBody'),saveMenu=document.getElementById('saveMenu');
 if(helpBody)helpBody.onmousewheel=function(e){return smoothWheelInput(helpBody,e,true);};
 if(saveMenu)saveMenu.onmousewheel=function(e){return smoothWheelInput(saveMenu,e,true);};
 thumb.onmousedown=function(e){
  e=e||window.event;stopSmoothScroll(pane);scrollDrag=true;scrollDragY=e.clientY;scrollDragTop=parseInt(thumb.style.top,10)||2;
  thumb.className='scroll-thumb dragging';if(e.preventDefault)e.preventDefault();return false;
 };
 track.onmousedown=function(e){
  e=e||window.event;if(e.srcElement===thumb||e.target===thumb)return;
  stopSmoothScroll(pane);
  var rect=track.getBoundingClientRect(),ratio=(e.clientY-rect.top)/track.clientHeight;
  pane.scrollTop=Math.max(0,Math.min(pane.scrollHeight-pane.clientHeight,ratio*(pane.scrollHeight-pane.clientHeight)));
 };
 document.onmousemove=function(e){
  if(itemSummaryScrollDrag){
   e=e||window.event;
   var summaryPane=document.getElementById('itemSummaryList'),summaryTrack=document.getElementById('itemSummaryScrollTrack'),summaryThumb=document.getElementById('itemSummaryScrollThumb');
   var summaryUsable=Math.max(0,summaryTrack.clientHeight-30-summaryThumb.offsetHeight);
   var summaryTop=Math.max(15,Math.min(15+summaryUsable,itemSummaryScrollDragTop+e.clientY-itemSummaryScrollDragY));
   summaryThumb.style.top=summaryTop+'px';
   summaryPane.scrollTop=summaryUsable>0?(summaryTop-15)/summaryUsable*(summaryPane.scrollHeight-summaryPane.clientHeight):0;
   return;
  }
  if(!scrollDrag)return;e=e||window.event;
  var usable=track.clientHeight-4-thumb.offsetHeight;
  var top=Math.max(2,Math.min(2+usable,scrollDragTop+e.clientY-scrollDragY));
  pane.scrollTop=usable>0?(top-2)/usable*(pane.scrollHeight-pane.clientHeight):0;
 };
 document.onmouseup=function(){
  if(itemSummaryScrollDrag){itemSummaryScrollDrag=false;updateItemSummaryScroll();}
  if(scrollDrag){scrollDrag=false;thumb.className='scroll-thumb';}
 };
 window.onresize=function(){
  stopSmoothScroll(pane);
  scheduleScrollUi();
 };
 updateScrollBar();
}

function esc(value){
 if(value===null||typeof value==='undefined')return '';
 return String(value).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""/g,'&quot;').replace(/'/g,'&#39;');
}
function parseResult(text){
 try{return JSON.parse(String(text));}catch(e){return {Success:false,Error:'The utility returned unreadable data: '+e.message};}
}
function busyPhase(value){
 if(value>=100)return 'COMPLETE';
 if(value>=82)return 'FINAL VERIFICATION';
 if(value>=58)return 'VALIDATING RESULTS';
 if(value>=30)return 'PROCESSING LOCAL DATA';
 if(value>=12)return 'READING FILES';
 return 'INITIALIZING';
}
function setBusyProgress(value,phase){
 busyProgressValue=Math.max(0,Math.min(100,Number(value)||0));
 var fill=document.getElementById('busyProgressFill'),percent=document.getElementById('busyPercent'),label=document.getElementById('busyPhase');
 if(fill)fill.style.width=Math.round(busyProgressValue)+'%';
 if(percent)percent.innerText=Math.round(busyProgressValue)+'%';
 if(label)label.innerText=phase||busyPhase(busyProgressValue);
}
function resetBusyProgress(){
 var fill=document.getElementById('busyProgressFill');
 if(fill){
  fill.style.transition='none';fill.style.width='0%';
  var ignored=fill.offsetWidth;
  fill.style.transition='';
 }
 busyProgressValue=0;
 var percent=document.getElementById('busyPercent'),label=document.getElementById('busyPhase');
 if(percent)percent.innerText='0%';if(label)label.innerText='INITIALIZING';
}
function startBusyProgress(){
 if(busyProgressTimer)window.clearInterval(busyProgressTimer);
 if(busyHideTimer)window.clearTimeout(busyHideTimer);
 var token=++busyProgressToken;
 resetBusyProgress();
 window.setTimeout(function(){if(token===busyProgressToken)setBusyProgress(4);},25);
 busyProgressTimer=window.setInterval(function(){
  if(token!==busyProgressToken)return;
  var ceiling=89;
  if(busyProgressValue>=ceiling)return;
  var step=busyProgressValue<28?4:(busyProgressValue<62?2:.65);
  setBusyProgress(Math.min(ceiling,busyProgressValue+step));
 },85);
}
function finishBusyProgress(){
 var token=busyProgressToken;
 if(busyProgressTimer){window.clearInterval(busyProgressTimer);busyProgressTimer=0;}
 setBusyProgress(100,'COMPLETE');
 busyHideTimer=window.setTimeout(function(){
  if(token!==busyProgressToken)return;
  document.getElementById('busy').className='busy';
  operationBusy=false;applyGameLock(gameRunning);resetBusyProgress();
  window.setTimeout(maybeShowPendingUpdate,180);
 },220);
}
function busyLeadDelay(){return 520;}
function busy(show,title,text){
 if(show){
  operationBusy=true;
  document.getElementById('busyTitle').innerText=title||'WORKING';
  document.getElementById('busyText').innerText=text||'Local operation in progress.';
  document.getElementById('busy').className='busy show';
  startBusyProgress();applyGameLock(gameRunning);
 }else{
  finishBusyProgress();
 }
}
function boot(){
 document.onclick=function(){closeSaveMenu();window.setTimeout(maybeShowPendingUpdate,180);};
 document.onkeydown=function(e){
  e=e||window.event;
  var key=e.keyCode||e.which;
  if(tutorialActive){
   if(key===27)skipTutorial();
   else if(key===37)previousTutorialStep();
   else if(key===39||key===13)nextTutorialStep();
   if(e.preventDefault)e.preventDefault();
   return false;
  }
  if(key===27&&document.getElementById('updateModal').className.indexOf('show')>=0){closeUpdateModal();return false;}
  if(key===27&&document.getElementById('itemSummaryModal').className.indexOf('show')>=0){closeItemSummary();return false;}
  if(key===27&&document.getElementById('itemClearModal').className.indexOf('show')>=0){closeItemClearConfirm();return false;}
  if(key===27&&document.getElementById('helpModal').className.indexOf('show')>=0){closeHelp();return false;}
  if(key===27&&document.getElementById('onboardModal').className.indexOf('show')>=0){declineTutorial();return false;}
   if(key===27&&document.getElementById('cannonDangerModal').className.indexOf('show')>=0){closeCannonDangerConfirm();return false;}
   if(key===27&&document.getElementById('developerCommandModal').className.indexOf('show')>=0){closeDeveloperCommandConfirm();return false;}
   if(key===27&&document.getElementById('dependencyModal').className.indexOf('show')>=0){closeDependencyConfirm();return false;}
  if(key===27&&document.getElementById('secretModsLayer').className.indexOf('show')>=0){closeSecretMods();return false;}
  if(key===27)closeHotfixConfirm();
 };
 setupScrollBar();
 loadAppUpdateState();
 loadSecretModsState();
 refreshSaves();
 window.setInterval(pollGameProcess,1000);
 window.setTimeout(checkFirstRunTutorial,650);
 window.setTimeout(function(){checkForUpdates(false);},1800);
 window.setInterval(function(){checkForUpdates(false);},1800000);
}
function loadPath(path){
 if(!path)return;
 var direct={Path:String(path),Name:fileName(String(path)),Modified:'OPENED DIRECTLY',Size:'',UserFolder:''};
 direct=upsertSave(direct);selectSave(direct);analyzeSelected();
}
function fileName(path){
 var slash=Math.max(path.lastIndexOf('\\'),path.lastIndexOf('/'));
 return slash>=0?path.substring(slash+1):path;
}
function refreshSaves(){
 var state=parseResult(window.external.Discover());
 gameRunning=!!state.GameRunning;
 saveItems=[];
 if(state.Success&&state.Saves&&state.Saves.length){
  for(var i=0;i<state.Saves.length;i++)saveItems.push(state.Saves[i]);
  renderSaveMenu();selectSave(saveItems[0]);
 }else{
  currentPath='';renderSaveMenu();
  setSaveDisplay('NO SURVIVAL SAVES FOUND','Use Browse to locate a Chapter 2 .db file');
  document.getElementById('pathText').innerText=state.Error||'Use Browse to locate a .db survival save.';
 }
 renderGameBanner(gameRunning);
 updateScrollBar();
}
function renderGameBanner(running){
 document.getElementById('gameBanner').innerHTML=running
  ?'<div class=""banner banner-error""><b>WORLD ACCESS SAFETY LOCKED.</b> Scrap Mechanic is running, so ScrapLab will not open any save database. Close the game to unlock the controls automatically.</div>':'';
 applyGameLock(running);
 updateScrollBar();
}
function applyGameLock(running){
 var locked=!!running||!!operationBusy;
 var analyze=document.getElementById('analyzeBtn');
 var browse=document.getElementById('browseBtn');
 var display=document.getElementById('saveDisplay');
 if(analyze)analyze.disabled=locked||!currentPath;
 if(browse)browse.disabled=locked;
 if(display)display.disabled=locked;
 if(running)closeSaveMenu();
 refreshWorldActionLocks();
 renderSecretModsState();
}
function refreshWorldActionLocks(){
 var locked=!!gameRunning||!!operationBusy;
 var scanItems=document.getElementById('scanDroppedItemsBtn');
 var scanPerformance=document.getElementById('scanPerformanceBtn');
 var repairCrops=document.getElementById('repairOrphanedCropsBtn');
 var clearRaids=document.getElementById('clearAllBtn');
 var clearExpired=document.getElementById('clearExpiredItemsBtn');
 var clearDropped=document.getElementById('clearDroppedItemsBtn');
 if(scanItems)scanItems.disabled=locked;
 if(scanPerformance)scanPerformance.disabled=locked;
 if(repairCrops)repairCrops.disabled=locked||!lastAnalysis||!lastAnalysis.CanRepairOrphanedCrops;
 if(clearRaids)clearRaids.disabled=locked||!lastAnalysis||!lastAnalysis.CanClear;
 if(clearExpired)clearExpired.disabled=locked||!lastAnalysis||!lastAnalysis.CanClearExpiredDroppedItems;
 if(clearDropped)clearDropped.disabled=locked||!lastAnalysis||!lastAnalysis.CanClearDroppedItems;
}
function ensureGameClosed(){
 var running=true;
 try{running=!!window.external.IsGameRunning();}catch(e){running=true;}
 gameRunning=running;
 renderGameBanner(running);
 if(running){
  setAnalysisGameState(true);
  return false;
 }
 return true;
}
function setAnalysisGameState(running){
 if(!lastAnalysis||!lastAnalysis.Success)return;
 var gameWarning='Scrap Mechanic is running. World analysis and repair controls are safety locked.';
 var warnings=[],existing=lastAnalysis.Warnings||[];
 for(var i=0;i<existing.length;i++)if(existing[i]!==gameWarning)warnings.push(existing[i]);
 if(running)warnings.push(gameWarning);
 lastAnalysis.Warnings=warnings;
 lastAnalysis.GameRunning=running;
 lastAnalysis.CanClear=!!(lastAnalysis.RaidManagerPresent&&lastAnalysis.RaidCount>0&&
  Number(lastAnalysis.UnreleasableRaidCropCount||0)===0&&
  String(lastAnalysis.DatabaseStatus).toLowerCase()==='ok'&&!running);
 lastAnalysis.CanRepairOrphanedCrops=!!(Number(lastAnalysis.OrphanedRaidCropCount||0)>0&&
  String(lastAnalysis.DatabaseStatus).toLowerCase()==='ok'&&!running);
 lastAnalysis.CanClearDroppedItems=!!(lastAnalysis.DroppedItemCount>0&&
  String(lastAnalysis.DatabaseStatus).toLowerCase()==='ok'&&!running);
 lastAnalysis.CanClearExpiredDroppedItems=!!(lastAnalysis.ExpiredDroppedItemCount>0&&
  String(lastAnalysis.DatabaseStatus).toLowerCase()==='ok'&&!running);
 renderAnalysis(lastAnalysis);
}
function pollGameProcess(){
 if(operationBusy||tutorialActive)return;
 try{
  var running=!!window.external.IsGameRunning();
  if(gameRunning===null){gameRunning=running;renderGameBanner(running);return;}
  if(running===gameRunning)return;
  var wasRunning=gameRunning;
  gameRunning=running;
  renderGameBanner(running);
  if(wasRunning&&!running&&currentPath){
   analyzeSelected(true);
  }else{
   setAnalysisGameState(running);
  }
 }catch(e){}
}
function toggleSaveMenu(event){
 if(event)event.cancelBubble=true;
 if(gameRunning||operationBusy)return;
 var picker=document.getElementById('savePicker');
 picker.className=picker.className.indexOf(' open')>=0?'save-picker':'save-picker open';
}
function closeSaveMenu(){
 stopSmoothScroll(document.getElementById('saveMenu'));
 document.getElementById('savePicker').className='save-picker';
}
function setSaveDisplay(name,meta){
 document.getElementById('saveName').innerText=name||'UNNAMED WORLD';
 document.getElementById('saveMeta').innerText=meta||'SURVIVAL SAVE';
}
function saveMeta(item){
 var parts=[];
 if(item.Modified)parts.push(item.Modified);
 if(item.Size)parts.push(item.Size);
 if(item.UserFolder)parts.push(item.UserFolder);
 return parts.join('  |  ')||'SURVIVAL SAVE';
}
function renderSaveMenu(){
 var menu=document.getElementById('saveMenu');menu.innerHTML='';
 if(!saveItems.length){menu.innerHTML='<div class=""save-empty"">NO AUTOMATIC SAVES FOUND &mdash; USE BROWSE</div>';return;}
 for(var i=0;i<saveItems.length;i++){
  var item=saveItems[i],row=document.createElement('div');
  row.className='save-option'+(samePath(item.Path,currentPath)?' active':'');
  row._saveData=item;
  row.innerHTML='<span class=""option-name"">'+esc(item.Name)+'</span><span class=""option-meta"">'+esc(saveMeta(item))+'</span>';
  row.onclick=function(event){
   if(event)event.cancelBubble=true;
   if(gameRunning||operationBusy)return;
   selectSave(this._saveData);
  };
  menu.appendChild(row);
 }
}
function samePath(a,b){return String(a||'').toLowerCase()===String(b||'').toLowerCase();}
function upsertSave(item){
 for(var i=0;i<saveItems.length;i++)if(samePath(saveItems[i].Path,item.Path))return saveItems[i];
 saveItems.unshift(item);renderSaveMenu();return item;
}
function selectSave(item){
 if(!item)return;
 if(performancePath&&!samePath(performancePath,item.Path||''))clearPerformanceState(false);
 currentPath=item.Path||'';
 setSaveDisplay(item.Name,saveMeta(item));
 document.getElementById('pathText').innerText=currentPath||'No file selected';
 closeSaveMenu();renderSaveMenu();
 applyGameLock(gameRunning);
}
function browseSave(){
 if(!ensureGameClosed()||operationBusy)return;
 var path=String(window.external.Browse());if(!path)return;
 var browsed=upsertSave({Path:path,Name:fileName(path),Modified:'BROWSED FILE',Size:'',UserFolder:''});
 selectSave(browsed);analyzeSelected();
}
function analyzeSelected(autoRefresh){
 if(!currentPath){showError('Choose a survival world first.');return;}
 if(!ensureGameClosed()||operationBusy)return;
 clearPerformanceState(false);
 busy(true,autoRefresh?'GAME CLOSED — REFRESHING':'DECODING WORLD STORAGE',
  autoRefresh?'Updating raid diagnostics and safe repair controls.':'Checking database integrity and stored raids. Loose items remain unscanned.');
 window.setTimeout(function(){
  if(!ensureGameClosed()){busy(false);return;}
  var data=parseResult(window.external.Analyze(currentPath));
  lastAnalysis=data;
  if(data.Success||data.GameRunning){
   gameRunning=!!data.GameRunning;
   renderGameBanner(gameRunning);
  }
  renderAnalysis(data);busy(false);
 },busyLeadDelay());
}
function scanDroppedItems(){
 if(!currentPath){showError('Choose and analyze a survival world first.');return;}
 if(!ensureGameClosed()||operationBusy)return;
 busy(true,'SCANNING LOOSE WORLD ITEMS','Decoding pickup storage, item values, icons, quantities, positions, and expiry timers.');
 window.setTimeout(function(){
  if(!ensureGameClosed()){busy(false);return;}
  var data=parseResult(window.external.ScanDroppedItems(currentPath));
  lastAnalysis=data;
  if(data.Success||data.GameRunning){
   gameRunning=!!data.GameRunning;
   renderGameBanner(gameRunning);
  }
  renderAnalysis(data);busy(false);
 },busyLeadDelay());
}
function clearPerformanceState(cancelRunning){
 if(performancePollTimer){window.clearTimeout(performancePollTimer);performancePollTimer=0;}
 if(cancelRunning&&performanceActive&&performanceOperationId){
  try{window.external.CancelPerformanceScan(performanceOperationId);}catch(ignore){}
 }
 if(performanceActive)operationBusy=false;
 performanceOperationId='';
 performanceStatus=null;
 performanceResult=null;
 performancePath='';
 performanceActive=false;
 performanceWorldFilter='all';
 performanceExplorerOpen=false;
 performanceExplorerPage=null;
 performanceExplorerWorldId=null;
 performanceExplorerOffset=0;
 performanceExportMessage='';
 performanceExportFailed=false;
 applyGameLock(gameRunning);
}
function beginPerformanceScan(){
 if(!currentPath||!lastAnalysis||!lastAnalysis.Success){
  showError('Choose and analyze a survival world first.');return;
 }
 if(!ensureGameClosed()||operationBusy)return;
 performancePath=currentPath;
 performanceActive=true;
 performanceWorldFilter='all';
 performanceExplorerOpen=false;
 performanceExplorerPage=null;
 performanceExplorerWorldId=null;
 performanceExplorerOffset=0;
 performanceExportMessage='';
 performanceExportFailed=false;
 operationBusy=true;
 performanceResult=null;
 performanceStatus={State:'queued',Terminal:false,CanCancel:true,
  Progress:{Stage:0,StageCount:6,StageLabel:'Queued',OverallPercent:0,
   Message:'Waiting for the scanner thread.'}};
 renderAnalysis(lastAnalysis);
 applyGameLock(gameRunning);
 var started;
 try{started=parseResult(window.external.BeginPerformanceScan(currentPath));}
 catch(e){started={Success:false,Error:e.message||'The performance scanner did not start.'};}
 if(!started.Success){
  performanceActive=false;operationBusy=false;
  performanceStatus={State:'failed',Terminal:true,CanCancel:false,
   Error:started.Error||'The performance scanner did not start.',
   Progress:{Stage:0,StageCount:6,StageLabel:'Could not start',OverallPercent:0,Message:''}};
  refreshPerformanceSection();applyGameLock(gameRunning);return;
 }
 performanceOperationId=started.OperationId||'';
 pollPerformanceScan();
}
function pollPerformanceScan(){
 if(!performanceActive||!performanceOperationId)return;
 var status;
 try{status=parseResult(window.external.GetPerformanceScanStatus(performanceOperationId));}
 catch(e){status={Success:false,Error:e.message||'The scanner status could not be read.',Terminal:true,State:'failed'};}
 if(!status.Success){
  performanceStatus={State:'failed',Terminal:true,CanCancel:false,
   Error:status.Error||'The scanner operation was lost.',
   Progress:(performanceStatus&&performanceStatus.Progress)||{Stage:0,StageCount:6,StageLabel:'Stopped',OverallPercent:0,Message:''}};
  performanceActive=false;operationBusy=false;
  refreshPerformanceSection();applyGameLock(gameRunning);return;
 }
 performanceStatus=status;
 if(status.Terminal){
  performanceActive=false;operationBusy=false;
  performanceResult=status.State==='completed'&&status.Result?status.Result:null;
  renderAnalysis(lastAnalysis);
  applyGameLock(gameRunning);
  return;
 }
 refreshPerformanceSection();
 applyGameLock(gameRunning);
 performancePollTimer=window.setTimeout(pollPerformanceScan,200);
}
function cancelPerformanceScan(){
 if(!performanceActive||!performanceOperationId)return;
 try{window.external.CancelPerformanceScan(performanceOperationId);}catch(ignore){}
 if(performanceStatus){
  performanceStatus.State='cancelling';
  performanceStatus.CanCancel=false;
  if(performanceStatus.Progress)performanceStatus.Progress.Message='Stopping safely after the current read.';
 }
 refreshPerformanceSection();
}
function refreshPerformanceSection(){
 var zone=document.getElementById('performanceZone');
 if(zone)zone.innerHTML=performanceSectionInner();
 updateScrollBar();
}
function performanceSection(data){
 return '<div class=""perf-zone"" id=""performanceZone"">'+performanceSectionInner(data)+'</div>';
}
function performanceSectionInner(){
 var state=performanceStatus?String(performanceStatus.State||'').toLowerCase():'idle';
 var body='',button='';
 if(performanceActive||state==='queued'||state==='running'||state==='cancelling'){
  var progress=performanceStatus&&performanceStatus.Progress?performanceStatus.Progress:{};
  var percent=Math.max(0,Math.min(100,Number(progress.OverallPercent)||0));
  var stage=Number(progress.Stage)||0,stageCount=Number(progress.StageCount)||6,stages='';
  var names=['LAYOUT','COUNTS','CELLS','WORLDS','RANKING','REPORT'];
  for(var i=1;i<=stageCount;i++){
   var cls=i<stage?' done':(i===stage?' current':'');
   stages+='<span class=""perf-stage'+cls+'"">'+esc(names[i-1]||('STAGE '+i))+'</span>';
  }
  body='<div class=""perf-progress-card""><div class=""perf-progress-top""><div class=""perf-progress-copy""><b>'+
   esc(progress.StageLabel||'Preparing scan')+'</b><span>'+esc(progress.Message||'Reading the selected save.')+'</span></div>'+
   '<strong class=""perf-progress-percent"">'+esc(percent)+'%</strong></div><div class=""perf-track""><div class=""perf-fill"" style=""width:'+
   esc(percent)+'%""></div></div><div class=""perf-stages"">'+stages+'</div><div class=""perf-cancel-row""><span>Read-only scan. Your save is never changed.</span>'+
   '<button type=""button"" class=""btn perf-cancel"" onclick=""cancelPerformanceScan()"" '+(state==='cancelling'||!performanceStatus.CanCancel?'disabled=""disabled""':'')+'>'+
   (state==='cancelling'?'CANCELLING...':'CANCEL SCAN')+'</button></div></div>';
 }else if(performanceResult&&performanceResult.Success){
  body=performanceResultBody(performanceResult);
  button='<button type=""button"" class=""btn"" id=""scanPerformanceBtn"" onclick=""beginPerformanceScan()"">SCAN AGAIN</button>';
 }else if(state==='failed'||state==='cancelled'){
  var title=state==='cancelled'?'SCAN CANCELLED':'SCAN DID NOT FINISH';
  var message=performanceStatus&&performanceStatus.Error?performanceStatus.Error:
   (state==='cancelled'?'No report was created. You can start again whenever you are ready.':'The performance report could not be created.');
  body='<div class=""perf-message '+(state==='failed'?'bad':'')+'""><b>'+esc(title)+'</b>'+esc(message)+'</div>';
  button='<button type=""button"" class=""btn btn-primary"" id=""scanPerformanceBtn"" onclick=""beginPerformanceScan()"">TRY AGAIN</button>';
 }else{
  body='<div class=""perf-message""><b>FIND WHERE THIS WORLD IS HEAVIEST</b>'+
   'Find unusually crowded 3-by-3 cell areas using supported local save records. Every result includes the evidence behind its severity.</div>';
  button='<button type=""button"" class=""btn btn-primary"" id=""scanPerformanceBtn"" onclick=""beginPerformanceScan()"" '+(operationBusy||gameRunning?'disabled=""disabled""':'')+'>SCAN PERFORMANCE</button>';
 }
 return '<div class=""perf-shell""><div class=""perf-head""><div class=""perf-title""><b>PERFORMANCE SCANNER</b>'+
  '<strong>WORLD DENSITY REPORT</strong><p>Read-only evidence from the selected survival database.</p></div>'+button+
  '</div><div class=""perf-body"">'+body+'</div></div>';
}
function performanceResultBody(result){
 var categories=result.Categories||[],worlds=result.Worlds||[],hotspots=result.Hotspots||[],maxCategory=1,html='';
 for(var i=0;i<categories.length;i++)maxCategory=Math.max(maxCategory,Number(categories[i].RecordCount)||0);
 html='<div class=""perf-summary"">'+performanceStat('WORLDS',result.WorldsScanned)+
  performanceStat('RECORDS',formatPerformanceNumber(result.TotalRecords))+
  performanceStat('POPULATED CELLS',formatPerformanceNumber(result.PopulatedCells))+
 performanceStat('POTENTIAL HOTSPOTS',formatPerformanceNumber(hotspots.length))+
 performanceStat('SCAN TIME',formatPerformanceDuration(result.DurationMilliseconds))+'</div>';
 html+=performanceReportTools();
 html+=performanceWorldFilters(worlds,hotspots)+performanceHotspotList(hotspots);
 html+=performanceExplorerPanel(result);
 html+='<div class=""perf-columns""><div class=""perf-column""><div class=""perf-subtitle"">SUPPORTED RECORD FAMILIES</div>';
 if(categories.length){
  for(var c=0;c<categories.length;c++){
   var category=categories[c],width=Math.max(2,Math.round((Number(category.RecordCount)||0)*100/maxCategory));
   html+='<div class=""perf-category""><div class=""perf-category-copy""><b>'+esc(category.DisplayName||category.Key||'Records')+
    '</b><span>'+esc(formatPerformanceBytes(category.PayloadBytes))+' stored</span><div class=""perf-category-bar""><i style=""width:'+
    esc(width)+'%""></i></div></div><strong class=""perf-category-value"">'+esc(formatPerformanceNumber(category.RecordCount))+'</strong></div>';
  }
 }else html+='<div class=""perf-message"">No supported record families were present.</div>';
 html+='</div><div class=""perf-column""><div class=""perf-subtitle"">WORLD TOTALS</div>';
 if(worlds.length){
  for(var w=0;w<worlds.length;w++){
   var world=worlds[w];
   html+='<div class=""perf-world""><div><b>'+esc(world.WorldName||('World '+world.WorldId))+'</b><span>'+
    esc(formatPerformanceNumber(world.PopulatedCells))+' populated cells &middot; '+esc(formatPerformanceBytes(world.TotalPayloadBytes))+
    '</span></div><strong>'+esc(formatPerformanceNumber(world.TotalRecords))+'</strong></div>';
  }
 }else html+='<div class=""perf-message"">No supported world records were present.</div>';
 html+='</div></div>';
 if(Number(result.UnsupportedTableCount)>0){
  var unsupported=result.UnsupportedTables||[],names=[];
  for(var u=0;u<unsupported.length;u++)names.push(esc(unsupported[u]));
  html+='<div class=""perf-limit""><b>PARTIAL SCHEMA COVERAGE:</b> '+esc(formatPerformanceNumber(result.UnsupportedTableCount))+
   ' unrecognized or not-yet-supported table(s) were safely excluded.'+
   (names.length?' Seen locally: '+names.join(', ')+'.':'')+'</div>';
 }
 html+='<div class=""perf-limit""><b>WHAT THIS PROVES:</b> the report measures supported save-record density and payload size. '+
  'It does not measure FPS, physics time, or guarantee that changing an area will improve performance. Coverage: '+
  esc(formatPerformancePercent(Number(result.Coverage||0)*100))+' of considered records were decoded by the current allowlist.</div>';
 return html;
}
function performanceReportTools(){
 var status=performanceExportMessage?'<span class=""perf-tool-status'+(performanceExportFailed?' bad':'')+'"">'+esc(performanceExportMessage)+'</span>':'';
 return '<div class=""perf-report-tools""><div class=""perf-tool-copy""><b>LOCAL REPORT TOOLS</b>'+
  '<span>Export a privacy-safe JSON summary or inspect paged aggregate cells. No save path or raw payload is included.</span>'+status+'</div>'+
  '<div class=""perf-tool-actions""><button type=""button"" class=""btn perf-export"" onclick=""exportPerformanceReport()"">EXPORT JSON</button>'+
  '<button type=""button"" class=""btn perf-explore"" onclick=""togglePerformanceExplorer()"">'+
  (performanceExplorerOpen?'CLOSE CELLS':'EXPLORE CELLS')+'</button></div></div>';
}
function exportPerformanceReport(){
 if(!performanceOperationId||!performanceResult)return;
 var result;
 try{result=parseResult(window.external.ExportPerformanceReport(performanceOperationId));}
 catch(e){result={Success:false,Error:e.message||'The report could not be exported.'};}
 if(result.Cancelled)return;
 performanceExportFailed=!result.Success;
 performanceExportMessage=result.Success
  ?'Saved '+String(result.FileName||'the performance report')+'.'
  :(result.Error||'The performance report could not be saved.');
 refreshPerformanceSection();
}
function togglePerformanceExplorer(){
 if(performanceExplorerOpen){
  performanceExplorerOpen=false;performanceExplorerPage=null;refreshPerformanceSection();return;
 }
 if(!performanceResult||!performanceOperationId)return;
 var worlds=performanceResult.Worlds||[],worldId=null;
 if(performanceWorldFilter!=='all')worldId=Number(performanceWorldFilter);
 if(worldId===null||isNaN(worldId)){
  for(var i=0;i<worlds.length;i++)if(Number(worlds[i].PopulatedCells)>0){worldId=Number(worlds[i].WorldId);break;}
 }
 performanceExplorerOpen=true;
 if(worldId===null||isNaN(worldId)){
  performanceExplorerPage={Success:false,Error:'No populated supported cells are available.',Cells:[]};
  refreshPerformanceSection();return;
 }
 loadPerformanceCells(worldId,0);
}
function loadPerformanceCells(worldId,offset){
 if(!performanceOperationId)return;
 performanceExplorerWorldId=Number(worldId);
 performanceExplorerOffset=Math.max(0,Number(offset)||0);
 var page;
 try{
  page=parseResult(window.external.GetPerformanceWorldCells(
   performanceOperationId,performanceExplorerWorldId,performanceExplorerOffset,performanceExplorerLimit));
 }catch(e){page={Success:false,Error:e.message||'The cell page could not be loaded.',Cells:[]};}
 performanceExplorerPage=page;
 refreshPerformanceSection();
}
function performanceExplorerPanel(result){
 if(!performanceExplorerOpen)return '';
 var worlds=result.Worlds||[],page=performanceExplorerPage||{},cells=page.Cells||[],worldButtons='',rows='';
 for(var i=0;i<worlds.length;i++){
  if(Number(worlds[i].PopulatedCells)<=0)continue;
  var worldId=Number(worlds[i].WorldId)||0,active=worldId===Number(performanceExplorerWorldId);
  worldButtons+='<button type=""button"" class=""perf-explorer-world'+(active?' active':'')+'"" onclick=""loadPerformanceCells('+esc(worldId)+',0)"">'+
   esc(worlds[i].WorldName||('World '+worldId))+' &middot; '+esc(formatPerformanceNumber(worlds[i].PopulatedCells))+'</button>';
 }
 if(!page.Success){
  rows='<div class=""perf-message bad""><b>CELL DATA UNAVAILABLE</b>'+esc(page.Error||'The aggregated cells could not be loaded.')+'</div>';
 }else if(!cells.length){
  rows='<div class=""perf-message""><b>NO CELLS ON THIS PAGE</b>This world has no supported aggregate cells at the requested offset.</div>';
 }else{
  for(var c=0;c<cells.length;c++)rows+=performanceCellRow(cells[c]);
 }
 var start=Number(page.TotalCells)>0?Number(page.Offset)+1:0,end=Math.min(Number(page.TotalCells)||0,(Number(page.Offset)||0)+cells.length);
 var previous=Math.max(0,(Number(page.Offset)||0)-(Number(page.Limit)||performanceExplorerLimit));
 var next=(Number(page.Offset)||0)+(Number(page.Limit)||performanceExplorerLimit);
 return '<div class=""perf-explorer"" role=""region"" aria-label=""Aggregated world cell explorer""><div class=""perf-explorer-head"">'+
  '<div><b>AGGREGATED CELL EXPLORER</b><span>Local, read-only pages from scanner version '+esc(result.ScanVersion)+'. Each row combines proven records in one cell.</span></div>'+
  '<button type=""button"" class=""btn perf-explore"" onclick=""togglePerformanceExplorer()"">CLOSE</button></div>'+
  '<div class=""perf-explorer-worlds"">'+worldButtons+'</div><div class=""perf-cell-list"">'+rows+'</div>'+
  '<div class=""perf-explorer-page""><span>SHOWING '+esc(start)+'&ndash;'+esc(end)+' OF '+esc(formatPerformanceNumber(page.TotalCells||0))+
  ' CELLS &middot; ORDERED BY CELL COORDINATES</span><div><button type=""button"" class=""btn"" onclick=""loadPerformanceCells('+esc(Number(performanceExplorerWorldId)||0)+','+esc(previous)+')"" '+
  ((Number(page.Offset)||0)<=0?'disabled=""disabled""':'')+'>PREVIOUS</button><button type=""button"" class=""btn"" onclick=""loadPerformanceCells('+
  esc(Number(performanceExplorerWorldId)||0)+','+esc(next)+')"" '+(!page.HasMore?'disabled=""disabled""':'')+'>NEXT</button></div></div></div>';
}
function performanceCellRow(cell){
 var categories=cell.Categories||[],parts=[];
 for(var i=0;i<categories.length;i++)parts.push(String(categories[i].DisplayName||categories[i].Key||'Records')+' '+formatPerformanceNumber(categories[i].RecordCount));
 return '<div class=""perf-cell""><div class=""perf-cell-coordinate""><b>CELL '+esc(cell.CellX)+', '+esc(cell.CellY)+'</b><span>WORLD CENTER X '+
  esc(Number(cell.ApproximateCenterX||0).toFixed(1))+' &middot; Y '+esc(Number(cell.ApproximateCenterY||0).toFixed(1))+'</span></div>'+
  '<div class=""perf-cell-metric""><b>'+esc(formatPerformanceNumber(cell.TotalRecords))+'</b><span>SUPPORTED RECORDS</span></div>'+
  '<div class=""perf-cell-metric""><b>'+esc(formatPerformanceBytes(cell.TotalPayloadBytes))+'</b><span>STORED PAYLOAD</span></div>'+
  '<div class=""perf-cell-categories""><b>'+esc(parts.join(' / ')||'No decoded categories')+'</b><span>AGGREGATED CATEGORY BREAKDOWN</span></div></div>';
}
function performanceWorldFilters(worlds,hotspots){
 var html='<div class=""perf-filters"" role=""group"" aria-label=""Filter performance hotspots by world""><span class=""perf-filter-label"">SHOW WORLD</span>'+
  '<button type=""button"" class=""perf-filter'+(performanceWorldFilter==='all'?' active':'')+'"" aria-pressed=""'+
  (performanceWorldFilter==='all')+'"" onclick=""selectPerformanceWorld(&quot;all&quot;)"">ALL ('+esc(hotspots.length)+')</button>';
 for(var i=0;i<worlds.length;i++){
  var world=worlds[i],count=Number(world.HotspotCount)||0;
  var key=String(world.WorldId),active=performanceWorldFilter===key;
  html+='<button type=""button"" class=""perf-filter'+(active?' active':'')+'"" aria-pressed=""'+active+
   '"" onclick=""selectPerformanceWorld('+esc(Number(world.WorldId)||0)+')"">'+
   esc(world.WorldName||('World '+world.WorldId))+' ('+esc(count)+')</button>';
 }
 return html+'</div>';
}
function selectPerformanceWorld(worldId){
 performanceWorldFilter=worldId==='all'?'all':String(worldId);
 refreshPerformanceSection();
}
function performanceHotspotList(hotspots){
 var html='<div class=""perf-hotspot-list"" role=""region"" aria-label=""Ranked potential performance hotspots"">',shown=0;
 for(var i=0;i<hotspots.length;i++){
  var hotspot=hotspots[i];
  if(performanceWorldFilter!=='all'&&String(hotspot.WorldId)!==performanceWorldFilter)continue;
  html+=performanceHotspotCard(hotspot);shown++;
 }
 if(!shown){
  html+='<div class=""perf-hotspot-empty""><b>NO UNUSUALLY DENSE SAVED AREAS WERE FOUND</b>'+
   (performanceWorldFilter==='all'
    ?'The supported records did not pass the conservative evidence floor.'
    :'This world has no ranked hotspot in the current report.')+'</div>';
 }
 return html+'</div>';
}
function performanceHotspotCard(hotspot){
 var severity=String(hotspot.Severity||'NOTABLE').toUpperCase(),severityClass=severity==='VERY HEAVY'?'very-heavy':(severity==='HEAVY'?'heavy':'notable');
 var center=hotspot.ApproximateCenter||{},evidence=hotspot.Evidence||[],categories=hotspot.Categories||[],evidenceHtml='',categoryHtml='';
 for(var i=0;i<evidence.length;i++){
 evidenceHtml+='<div class=""perf-evidence-item""><div><b>'+esc(evidence[i].Label||'Measured evidence')+
   '</b><span>'+esc(evidence[i].Explanation||'')+'</span><em>'+esc(performanceEvidenceMeasure(evidence[i]))+'</em></div></div>';
 }
 for(var c=0;c<categories.length;c++){
  var category=categories[c],share=Number(hotspot.TotalRecords)>0?Math.round(Number(category.RecordCount||0)*100/Number(hotspot.TotalRecords)):0;
  categoryHtml+='<div class=""perf-hotspot-category""><b>'+esc(category.DisplayName||category.Key||'Supported records')+
   ' &middot; '+esc(formatPerformanceNumber(category.RecordCount))+'</b><span>'+esc(formatPerformanceBytes(category.PayloadBytes))+
   ' across this neighborhood</span><div class=""perf-hotspot-category-line""><i style=""width:'+esc(Math.max(2,Math.min(100,share)))+'%""></i></div></div>';
 }
 var comparison=Math.max(0,Math.min(99.9,(Number(hotspot.Percentile)||0)-0.1));
 var delay=Math.min(12,Math.max(0,(Number(hotspot.Rank)||1)-1))*35;
 return '<article class=""perf-hotspot '+severityClass+'"" style=""animation-delay:'+esc(delay)+'ms"">'+
  '<div class=""perf-hotspot-head""><div class=""perf-rank""><span>#'+esc(hotspot.Rank)+'</span></div>'+
  '<div class=""perf-hotspot-title""><b>'+esc(hotspot.WorldName||('World '+hotspot.WorldId))+' &middot; CELL '+
  esc(hotspot.CellX)+', '+esc(hotspot.CellY)+'</b><span>WORLD RANK #'+esc(hotspot.WorldRank)+' &middot; CENTERED 3&times;3 NEIGHBORHOOD</span></div>'+
  '<span class=""perf-severity '+severityClass+'"">'+esc(severity)+'</span><span class=""perf-confidence"">'+esc(hotspot.Confidence||'RAW DATA ONLY')+' CONFIDENCE</span></div>'+
  '<div class=""perf-hotspot-body""><div class=""perf-hotspot-metrics"">'+
  performanceHotspotMetric('NEIGHBORHOOD RECORDS',formatPerformanceNumber(hotspot.NeighborhoodRecords||hotspot.TotalRecords),'')+
  performanceHotspotMetric('STORED PAYLOAD',formatPerformanceBytes(hotspot.NeighborhoodPayloadBytes||hotspot.TotalPayloadBytes),'')+
  performanceHotspotMetric('CENTER CELL RECORDS',formatPerformanceNumber(hotspot.CenterRecords),'')+
  performanceHotspotMetric('CELL COORDINATES',hotspot.CellX+', '+hotspot.CellY,'coordinate')+
  performanceHotspotMetric('APPROX. WORLD CENTER','X '+Number(center.X||0).toFixed(1)+'  Y '+Number(center.Y||0).toFixed(1),'coordinate')+
  '</div><div class=""perf-hotspot-compare""><b>WORLD COMPARISON:</b> Heavier than '+esc(comparison.toFixed(1))+
  '% of populated cells in this world using separately ranked record and byte totals.</div>'+
  '<div class=""perf-evidence"">'+evidenceHtml+'</div><div class=""perf-hotspot-foot"">'+
  (categoryHtml||'<div class=""perf-hotspot-category""><b>RAW COUNTS ONLY</b><span>No decoded category breakdown is available.</span></div>')+
  '<button type=""button"" class=""btn perf-copy"" id=""perfCopy'+esc(hotspot.Rank)+'"" onclick=""copyHotspotCoordinates('+esc(hotspot.Rank)+')"">COPY WORLD CENTER</button>'+
  '</div></div></article>';
}
function performanceEvidenceMeasure(evidence){
 var key=String(evidence.Key||''),observed=Number(evidence.ObservedValue)||0,threshold=Number(evidence.ComparisonValue)||0;
 if(key==='world-percentile')return 'MEASURED '+(observed/100).toFixed(1)+'%  /  FLOOR '+(threshold/100).toFixed(1)+'%';
 if(key==='stored-payload')return 'MEASURED '+formatPerformanceBytes(observed)+'  /  FLOOR '+formatPerformanceBytes(threshold);
 return 'MEASURED '+formatPerformanceNumber(observed)+'  /  FLOOR '+formatPerformanceNumber(threshold);
}
function performanceHotspotMetric(label,value,cls){
 return '<div class=""perf-hotspot-metric '+esc(cls||'')+'""><b>'+esc(label)+'</b><strong>'+esc(value)+'</strong></div>';
}
function copyHotspotCoordinates(rank){
 if(!performanceResult||!performanceResult.Hotspots)return;
 var hotspots=performanceResult.Hotspots,hotspot=null;
 for(var i=0;i<hotspots.length;i++)if(Number(hotspots[i].Rank)===Number(rank)){hotspot=hotspots[i];break;}
 if(!hotspot||!hotspot.ApproximateCenter)return;
 var center=hotspot.ApproximateCenter;
 var text=Number(center.X||0).toFixed(1)+', '+Number(center.Y||0).toFixed(1);
 var copied=false;
 try{copied=!!window.external.CopyText(text);}catch(ignore){}
 var button=document.getElementById('perfCopy'+rank);
 if(button){
  button.innerText=copied?'COPIED WORLD CENTER':'COPY FAILED';
  button.className='btn perf-copy'+(copied?' copied':'');
  window.setTimeout(function(){
   var current=document.getElementById('perfCopy'+rank);
   if(current){current.innerText='COPY WORLD CENTER';current.className='btn perf-copy';}
  },1600);
 }
}
function formatPerformancePercent(value){
 var numeric=Number(value)||0;
 return numeric.toFixed(numeric>=99.95?0:1)+'%';
}
function performanceStat(label,value){
 return '<div class=""perf-stat""><b>'+esc(label)+'</b><strong>'+esc(value===null||typeof value==='undefined'?'0':value)+'</strong></div>';
}
function formatPerformanceNumber(value){
 var numberValue=Number(value)||0;
 return String(Math.round(numberValue)).replace(/\B(?=(\d{3})+(?!\d))/g,',');
}
function formatPerformanceBytes(value){
 var bytes=Number(value)||0;
 if(bytes>=1073741824)return (bytes/1073741824).toFixed(1)+' GB';
 if(bytes>=1048576)return (bytes/1048576).toFixed(1)+' MB';
 if(bytes>=1024)return (bytes/1024).toFixed(1)+' KB';
 return Math.round(bytes)+' B';
}
function formatPerformanceDuration(milliseconds){
 var seconds=(Number(milliseconds)||0)/1000;
 return seconds<1?Math.max(1,Math.round(Number(milliseconds)||0))+' MS':seconds.toFixed(seconds<10?1:0)+' S';
}
function showError(message){
 document.getElementById('result').innerHTML='<div class=""banner banner-error""><b>DIAGNOSTIC FAILED.</b> '+esc(message)+'</div>';
 updateScrollBar();
}
function number(value){return value===null||typeof value==='undefined'?'—':String(value);}
function pos(center){
 if(!center)return 'NOT STORED';
 return Number(center.X).toFixed(1)+', '+Number(center.Y).toFixed(1)+', '+Number(center.Z).toFixed(1);
}
function renderAnalysis(data){
 if(!data.Success){showError(data.Error||'Unknown error');return;}
 var statusOk=String(data.DatabaseStatus).toLowerCase()==='ok',html='';
 if(data.Warnings){for(var w=0;w<data.Warnings.length;w++)html+='<div class=""banner banner-warn"">'+esc(data.Warnings[w])+'</div>';}
 html+='<div class=""stats"">'+
  stat('DATABASE',statusOk?'HEALTHY':data.DatabaseStatus,statusOk?'ok':'bad',true)+
  stat('SAVE VERSION',number(data.SaveVersion),'',false)+
  stat('GAME TICK',number(data.GameTick),'',false)+
  stat('STORED RAIDS',number(data.RaidCount),data.RaidCount?'accent':'ok',false)+
  stat('ORPHANED CROPS',number(data.OrphanedRaidCropCount||0),data.OrphanedRaidCropCount?'bad':'ok',false)+
  stat('LOOSE DROPS',data.DroppedItemsScanned?number(data.DroppedItemCount):'NOT SCANNED',data.DroppedItemsScanned?(data.DroppedItemCount?'accent':'ok'):'',true)+
  stat('WORLD SIZE',data.Size,'',true)+'</div>';
 if(data.Raids&&data.Raids.length){
  for(var i=0;i<data.Raids.length;i++)html+=raidCard(data.Raids[i]);
 }else{
  html+='<div class=""empty""><div class=""diamond""><span>&#10003;</span></div><h4>RAID STORAGE CLEAR</h4><p>No persisted raid-manager entries were found in this world.</p></div>';
 }
 html+=droppedItemsSection(data);
 html+=performanceSection(data);
  var orphaned=Number(data.OrphanedRaidCropCount||0);
  html+='<div class=""repair-bar"" id=""repairActionsBar""><p><b>BACKUP-FIRST RECOVERY</b><br/>Resolve stored raids without stranding crop growth, or repair crops left waiting by an older clear. ScrapLab verifies a backup before either repair.</p>'+
   '<div class=""repair-actions"">'+
   '<button class=""btn btn-primary"" id=""repairOrphanedCropsBtn"" '+(data.CanRepairOrphanedCrops&&!operationBusy?'':'disabled=""disabled""')+
   ' onclick=""repairOrphanedCrops()"">REPAIR ORPHANED CROPS'+(orphaned?' ('+esc(orphaned)+')':'')+'</button>'+
   '<button class=""btn btn-danger"" id=""clearAllBtn"" '+(data.CanClear&&!operationBusy?'':'disabled=""disabled""')+
   ' onclick=""clearRaids()"">RESOLVE &amp; CLEAR RAIDS</button></div></div>';
 document.getElementById('result').innerHTML=html;
 updateScrollBar();
}
function stat(label,value,cls,small){
 return '<div class=""stat""><div class=""label"">'+esc(label)+'</div><div class=""value '+(small?'small ':'')+esc(cls||'')+'"">'+esc(value)+'</div></div>';
}
function droppedItemsSection(data){
 if(!data.DroppedItemsScanned){
  return '<div class=""drop-zone"" id=""droppedItemsZone""><div class=""drop-scan-panel"">'+
   '<b>LOOSE ITEMS HAVE NOT BEEN SCANNED</b><p>Scan this world to view dropped-item cards, totals, despawn timers, and safe cleanup options.</p>'+
   '<button type=""button"" class=""btn btn-primary"" id=""scanDroppedItemsBtn"" onclick=""scanDroppedItems()"" '+(operationBusy||gameRunning?'disabled=""disabled""':'')+'>SCAN LOOSE ITEMS</button></div></div>';
 }
 var items=data.DroppedItems||[],count=Number(data.DroppedItemCount)||items.length,quantity=Number(data.DroppedItemQuantity)||0;
 var html='<div class=""drop-zone"" id=""droppedItemsZone""><div class=""drop-zone-head""><div class=""drop-zone-title"">'+
 '<b>VALUE-SORTED RECOVERY</b><strong>DROPPED ITEMS IN THIS WORLD</strong></div><div class=""drop-zone-summary"">'+
 '<span class=""drop-count""><b>'+esc(count)+'</b> STACK'+(count===1?'':'S')+'</span>'+
  '<span class=""drop-count""><b>'+esc(quantity)+'</b> ITEM'+(quantity===1?'':'S')+'</span>'+
  '<button type=""button"" class=""drop-collapse'+(droppedItemsCollapsed?' is-collapsed':'')+'"" id=""dropCollapseBtn"" '+
  'onclick=""toggleDroppedItems()"" title=""'+(droppedItemsCollapsed?'Expand item list':'Collapse item list')+'"" '+
  'aria-label=""'+(droppedItemsCollapsed?'Expand item list':'Collapse item list')+'"" aria-expanded=""'+(!droppedItemsCollapsed)+'"">'+
  '<svg viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M5 15 L12 8 L19 15""></path></svg></button>'+
  '<button type=""button"" class=""btn btn-summary"" id=""itemSummaryBtn"" onclick=""openItemSummary()"" '+(items.length?'':'disabled=""disabled""')+'>ITEM TOTALS</button>'+
  '<button type=""button"" class=""btn btn-expired"" id=""clearExpiredItemsBtn"" onclick=""requestExpiredDroppedItemClear()"" '+
  (data.CanClearExpiredDroppedItems&&!operationBusy?'':'disabled=""disabled""')+'>CLEAR EXPIRED ('+esc(data.ExpiredDroppedItemCount||0)+')</button>'+
  '<button type=""button"" class=""btn btn-danger"" id=""clearDroppedItemsBtn"" onclick=""requestDroppedItemClear(0)"" '+
  (data.CanClearDroppedItems&&!operationBusy?'':'disabled=""disabled""')+'>CLEAR ALL DROPPED ITEMS</button></div></div>'+
  '<div class=""drop-items-body"" id=""droppedItemsBody""'+(droppedItemsCollapsed?' hidden=""hidden""':'')+'>';
 if(items.length){
  html+='<div class=""drop-grid"">';
  for(var i=0;i<items.length;i++)html+=droppedItemCard(items[i],!!data.CanClearDroppedItems&&!operationBusy,data.DroppedItemIcons||{});
  html+='</div>';
 }else{
  html+='<div class=""drop-empty"">NO LOOSE INVENTORY PICKUPS ARE STORED IN THIS WORLD.</div>';
 }
 if(Number(data.UnreadableDroppedItemCount)>0)html+='<div class=""drop-warning""><b>SAFE SKIP:</b> '+
  esc(data.UnreadableDroppedItemCount)+' loose record(s) could not be proven safe and were excluded from removal.</div>';
 return html+'</div></div>';
}
function toggleDroppedItems(){
 droppedItemsCollapsed=!droppedItemsCollapsed;
 var body=document.getElementById('droppedItemsBody'),button=document.getElementById('dropCollapseBtn');
 if(body)body.hidden=droppedItemsCollapsed;
 if(button){
  button.className='drop-collapse'+(droppedItemsCollapsed?' is-collapsed':'');
  button.title=droppedItemsCollapsed?'Expand item list':'Collapse item list';
  button.setAttribute('aria-label',button.title);
  button.setAttribute('aria-expanded',String(!droppedItemsCollapsed));
 }
 updateScrollBar();
}
function droppedItemCard(item,canRemove,icons){
 var iconUrl=droppedIconUrl(item,icons),icon=iconUrl?
  '<img class=""drop-icon"" src=""'+esc(iconUrl)+'"" alt="""" />':
  '<span class=""drop-icon-fallback"">'+esc(String(item.Name||'?').charAt(0).toUpperCase()||'?')+'</span>';
 var remaining=droppedLifetime(item),lifeClass=item.Expired?' expired':(Number(item.RemainingSeconds)>0&&Number(item.RemainingSeconds)<=300?' soon':'');
 var description=item.Description||'Loose Scrap Mechanic inventory pickup.';
 var details='<span class=""drop-detail drop-value""><b>'+esc(item.ValueTier||'STANDARD')+'</b></span>'+
  '<span class=""drop-detail"">WORLD <b>'+esc(item.WorldName||('World '+item.WorldId))+'</b></span>'+
  '<span class=""drop-detail"">XYZ <b>'+esc(shortPosition(item.Position))+'</b></span>';
 if(item.Epic)details+='<span class=""drop-detail""><b>EPIC</b></span>';
 if(item.QuestItem)details+='<span class=""drop-detail""><b>QUEST ITEM</b></span>';
 return '<div class=""drop-wrap""><div class=""drop-card""><div class=""drop-icon-frame"">'+icon+
  '<span class=""drop-quantity"">&times;'+esc(item.Quantity)+'</span></div>'+
  '<button type=""button"" class=""drop-remove"" onclick=""requestDroppedItemClear('+esc(item.EntityId)+')"" '+
  (canRemove?'':'disabled=""disabled""')+'>REMOVE ITEM</button><div class=""drop-copy"">'+
  '<span class=""drop-kind"">'+esc(item.DropType||'Loose pickup')+'</span><span class=""drop-name"">'+esc(item.Name||'Unknown item')+'</span>'+
  '<p class=""drop-description"">'+esc(description)+'</p><span class=""drop-life'+lifeClass+'"">'+esc(remaining)+'</span>'+
  '<div class=""drop-detail-row"">'+details+'</div></div></div></div>';
}
function droppedIconUrl(item,icons){
 if(!item||!icons)return '';
 return icons[item.Uuid]||icons[String(item.Uuid||'').toLowerCase()]||'';
}
function shortPosition(position){
 if(!position)return 'NOT STORED';
 return Number(position.X).toFixed(1)+', '+Number(position.Y).toFixed(1)+', '+Number(position.Z).toFixed(1);
}
function droppedLifetime(item){
 if(item.Expired)return 'EXPIRED - PENDING WORLD CLEANUP';
 var seconds=Number(item.RemainingSeconds)||0;
 if(!item.KillTick)return 'NO EXPIRY TICK STORED';
 if(seconds<=0)return 'EXPIRES WHEN WORLD RESUMES';
 var hours=Math.floor(seconds/3600),minutes=Math.floor((seconds%3600)/60),secs=Math.floor(seconds%60),parts=[];
 if(hours)parts.push(hours+'H');
 if(minutes||hours)parts.push(minutes+'M');
 parts.push(secs+'S');
 return 'DESPAWNS IN '+parts.join(' ');
}
function openItemSummary(){
 if(operationBusy||!lastAnalysis||!lastAnalysis.DroppedItemsScanned)return;
 var items=lastAnalysis.DroppedItems||[],groups={},totalQuantity=0,expiredStacks=0;
 for(var i=0;i<items.length;i++){
  var item=items[i],key=String(item.Uuid||item.Name||('item-'+i)).toLowerCase(),group=groups[key];
  if(!group){
   group=groups[key]={Uuid:item.Uuid,Name:item.Name||'Unknown item',Quantity:0,Stacks:0,
    ValueScore:Number(item.ValueScore)||0,ValueTier:item.ValueTier||'STANDARD'};
  }
  group.Quantity+=Number(item.Quantity)||0;group.Stacks++;
  group.ValueScore=Math.max(group.ValueScore,Number(item.ValueScore)||0);
  if(item.Expired)expiredStacks++;
  totalQuantity+=Number(item.Quantity)||0;
 }
 var totals=[];
 for(var key in groups)if(groups.hasOwnProperty(key))totals.push(groups[key]);
 totals.sort(function(a,b){
  return b.ValueScore-a.ValueScore||b.Quantity-a.Quantity||
   String(a.Name).localeCompare(String(b.Name));
 });
 document.getElementById('itemSummaryStats').innerHTML=
  itemSummaryStat('UNIQUE ITEMS',totals.length)+itemSummaryStat('TOTAL QUANTITY',totalQuantity)+
  itemSummaryStat('LOOSE STACKS',items.length)+itemSummaryStat('EXPIRED STACKS',expiredStacks);
 var html='';
 for(var j=0;j<totals.length;j++){
  var total=totals[j],iconUrl=droppedIconUrl(total,lastAnalysis.DroppedItemIcons||{}),icon=iconUrl?
   '<img src=""'+esc(iconUrl)+'"" alt="""" />':
   '<span>'+esc(String(total.Name||'?').charAt(0).toUpperCase()||'?')+'</span>';
  html+='<div class=""item-summary-row""><div class=""item-summary-icon"">'+icon+'</div>'+
   '<div class=""item-summary-copy""><b>'+esc(total.Name)+'</b><span>'+esc(total.ValueTier)+
   ' - '+esc(total.Stacks)+' STACK'+(total.Stacks===1?'':'S')+'</span></div>'+
   '<div class=""item-summary-amount""><strong>&times;'+esc(total.Quantity)+'</strong><span>TOTAL ITEMS</span></div></div>';
 }
 var summaryList=document.getElementById('itemSummaryList');
 summaryList.innerHTML=html||'<div class=""item-summary-empty"">NO LOOSE ITEMS WERE FOUND IN THIS WORLD.</div>';
 summaryList.scrollTop=0;
 summaryList.onscroll=updateItemSummaryScroll;
 summaryList.onmousewheel=function(e){return smoothWheelInput(summaryList,e,true);};
 document.getElementById('itemSummaryModal').className='hotfix-modal item-summary-modal show';
 window.setTimeout(updateItemSummaryScroll,30);
}
function itemSummaryStat(label,value){
 return '<div class=""item-summary-stat""><b>'+esc(label)+'</b><strong>'+esc(value)+'</strong></div>';
}
function updateItemSummaryScroll(){
 var pane=document.getElementById('itemSummaryList'),track=document.getElementById('itemSummaryScrollTrack'),thumb=document.getElementById('itemSummaryScrollThumb');
 if(!pane||!track||!thumb)return;
 var viewport=pane.clientHeight,total=pane.scrollHeight,usable=Math.max(1,track.clientHeight-30);
 if(total<=viewport+1){
  thumb.className='item-summary-scroll-thumb disabled';thumb.style.height=Math.max(38,usable)+'px';thumb.style.top='15px';return;
 }
 thumb.className='item-summary-scroll-thumb';
 var height=Math.max(38,Math.floor(usable*viewport/total)),travel=Math.max(0,usable-height),maximum=total-viewport;
 thumb.style.height=height+'px';
 thumb.style.top=(15+(maximum>0?Math.round(travel*pane.scrollTop/maximum):0))+'px';
}
function itemSummaryThumbDown(e){
 e=e||window.event;var thumb=document.getElementById('itemSummaryScrollThumb');
 if(String(thumb.className).indexOf('disabled')>=0)return false;
 stopSmoothScroll(document.getElementById('itemSummaryList'));
 itemSummaryScrollDrag=true;itemSummaryScrollDragY=e.clientY;itemSummaryScrollDragTop=parseInt(thumb.style.top,10)||15;
 if(e.stopPropagation)e.stopPropagation();e.cancelBubble=true;if(e.preventDefault)e.preventDefault();return false;
}
function itemSummaryTrackDown(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('itemSummaryScrollThumb'))return false;
 var pane=document.getElementById('itemSummaryList'),track=document.getElementById('itemSummaryScrollTrack'),thumb=document.getElementById('itemSummaryScrollThumb');
 stopSmoothScroll(pane);
 var rect=track.getBoundingClientRect(),usable=Math.max(1,track.clientHeight-30),maximum=Math.max(0,pane.scrollHeight-pane.clientHeight);
 var ratio=Math.max(0,Math.min(1,(e.clientY-rect.top-15)/usable));
 pane.scrollTop=ratio*maximum;updateItemSummaryScroll();
 if(e.preventDefault)e.preventDefault();return false;
}
function closeItemSummary(){
 stopSmoothScroll(document.getElementById('itemSummaryList'));
 itemSummaryScrollDrag=false;
 document.getElementById('itemSummaryModal').className='hotfix-modal item-summary-modal';
}
function itemSummaryBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('itemSummaryModal'))closeItemSummary();
}
function raidMeter(tier){
 var value=Number(tier)||0,superRaid=value>5,html='<div class=""raid-meter"">';
 for(var i=1;i<=5;i++){
  var on=superRaid||i<=value;
  html+='<span class=""meter-seg '+(on?'on ':'')+(superRaid?'super':'s'+i)+'""></span>';
 }
 return html+'</div>';
}
function raidCard(r){
 var html='<div class=""raid""><div class=""raid-head""><div class=""tier-badge""><svg viewBox=""0 0 64 64"" width=""58"" height=""58"" aria-label=""Raid tier '+esc(r.Tier)+'"">'+
  '<polygon class=""tier-shape"" points=""32,3 61,32 32,61 3,32""></polygon><text class=""tier-number"" x=""32"" y=""39"">'+esc(r.Tier)+'</text></svg></div>'+
  '<div class=""raid-name""><h4>RAID '+esc(r.Number)+' &middot; TIER '+esc(r.Tier)+'</h4><p>'+esc(r.Key)+'</p></div>'+
  '<div class=""raid-meter-wrap""><div class=""meter-label""><span>RAID THREAT</span><span>'+esc(r.ThreatValue)+' / '+esc(r.MaximumThreatValue)+'</span></div>'+raidMeter(r.Tier)+'</div>'+
  '<div class=""state"">'+esc(r.State).toUpperCase()+'</div></div><div class=""raid-body"">';
 html+='<div class=""mini-grid"">'+
  mini('PLANNED ROBOTS',r.PlannedEnemyCount)+mini('SPAWN GROUPS',r.SpawnGroups)+
  mini('THREAT VALUE',r.ThreatValue+' / '+r.MaximumThreatValue)+mini('WORLD',r.WorldName||('World '+r.WorldSlot))+mini('CENTER',pos(r.Center))+
  mini('STORED CROPS',r.TrackedCrops)+mini('STALE CROPS',r.StaleCropReferences)+
  mini('LIVE RAIDERS',r.LiveRaiderReferences)+mini('TICK COUNTER',r.TickCounter)+mini('TIMEOUT TICK',r.TimeoutTick)+'</div>';
 html+='<div class=""section-label"">ROBOT WAVE COMPOSITION</div><div class=""chips"">'+chips(r.Enemies,false,'NO DECODED ENEMY GROUPS')+'</div>';
 html+='<div class=""section-label"">CROPS REGISTERED TO THIS RAID</div><div class=""chips"">'+chips(r.Crops,true,'NO CROP HISTORY STORED')+'</div>';
 html+='<div class=""section-label"">STORED TIMING VALUES</div><div class=""chips"">'+
  rawChip('savedTick',r.SavedTick)+rawChip('lastSpawnTick',r.LastSpawnTick)+rawChip('needsSpawnPoints',r.NeedsSpawnPoints?'true':'false')+
  rawChip('planting records',r.PlantingRecords)+'</div>';
 if(r.LooksStuck)html+='<div class=""note""><b>LIKELY PERMANENT RAID:</b> every saved crop reference points to a missing harvestable while the raid record remains active.</div>';
 if(r.Notes){for(var n=0;n<r.Notes.length;n++)html+='<div class=""note"">'+esc(r.Notes[n])+'</div>';}
 return html+'</div></div>';
}
function mini(label,value){return '<div class=""mini""><div><span>'+esc(label)+'</span><strong>'+esc(value)+'</strong></div></div>';}
function rawChip(label,value){return '<span class=""chip raw"">'+esc(label)+' <b>'+esc(value)+'</b></span>';}
function chips(items,isCrop,empty){
 if(!items||!items.length)return '<span class=""chip"">'+esc(empty)+'</span>';
 var html='';
 for(var i=0;i<items.length;i++)html+='<span class=""chip '+(isCrop?'crop':'')+'"">'+
  esc(items[i].Name).toUpperCase()+' <b>&times;'+esc(items[i].Quantity)+'</b></span>';
 return html;
}
function requestExpiredDroppedItemClear(){
 requestDroppedItemClear(0,true);
}
function requestDroppedItemClear(entityId,expiredOnly){
 expiredOnly=!!expiredOnly;
 if(operationBusy||!lastAnalysis||
  (expiredOnly?!lastAnalysis.CanClearExpiredDroppedItems:!lastAnalysis.CanClearDroppedItems)||
  !ensureGameClosed())return;
 pendingDroppedEntityId=Number(entityId)||0;
 pendingDroppedItem=null;
 pendingDroppedMode=expiredOnly?'expired':(pendingDroppedEntityId?'one':'all');
 var allItems=lastAnalysis.DroppedItems||[],items=[];
 for(var i=0;i<allItems.length;i++)if(!expiredOnly||allItems[i].Expired)items.push(allItems[i]);
 if(pendingDroppedEntityId){
  for(var j=0;j<allItems.length;j++)if(Number(allItems[j].EntityId)===pendingDroppedEntityId){pendingDroppedItem=allItems[j];break;}
  if(!pendingDroppedItem){showError('That loose item is no longer in the current analysis. Analyze the world again.');return;}
 }
 var mode=pendingDroppedMode,title=mode==='expired'?'CLEAR EXPIRED LOOSE ITEMS?':
  (mode==='all'?'CLEAR EVERY LOOSE WORLD ITEM?':'REMOVE THIS LOOSE WORLD ITEM?');
 var kicker='';
 kicker=mode==='expired'?'EXPIRED PICKUPS - '+items.length+' STACKS':
  (mode==='all'?'ALL DECODED PICKUPS - '+items.length+' STACKS':'ENTITY #'+pendingDroppedEntityId+' - BACKUP-FIRST REMOVAL');
 document.getElementById('itemClearTitle').innerText=title;
 document.getElementById('itemClearKicker').innerText=kicker;
 document.getElementById('itemClearIntro').innerText=mode==='expired'?
  'ScrapLab will remove only loose pickups marked Expired - Pending World Cleanup.':
  (mode==='all'?'ScrapLab will remove every safely decoded loose pickup shown in this report.':
  'ScrapLab will remove only this loose pickup and its matching Lua storage record.');
 document.getElementById('itemClearConfirmButton').innerHTML='<span>!</span>'+
  (mode==='expired'?'CLEAR EXPIRED ITEMS':(mode==='all'?'CLEAR ALL DROPPED ITEMS':'REMOVE THIS DROP'));
 document.getElementById('itemClearPreview').innerHTML=itemClearPreview(mode,items);
 document.getElementById('itemClearModal').className='hotfix-modal item-clear-modal show';
 window.setTimeout(function(){document.getElementById('itemClearConfirmButton').focus();},30);
}
function itemClearPreview(mode,items){
 if(mode!=='one'){
  var quantity=0;
  for(var i=0;i<items.length;i++)quantity+=Number(items[i].Quantity)||0;
  return '<div class=""item-confirm-icon""><span class=""item-confirm-fallback"">'+esc(items.length)+'</span></div>'+
   '<div class=""item-confirm-copy""><b>'+(mode==='expired'?'EXPIRED PICKUPS ONLY':'ALL LOOSE PICKUPS')+'</b><span>'+
   esc(quantity)+' total item(s) across '+esc(items.length)+
   ' stack(s). Placed creations and player inventories are not included.</span></div>';
 }
 var item=pendingDroppedItem||{},iconUrl=droppedIconUrl(item,lastAnalysis.DroppedItemIcons||{}),icon=iconUrl?
  '<img src=""'+esc(iconUrl)+'"" alt="""" />':
  '<span class=""item-confirm-fallback"">'+esc(String(item.Name||'?').charAt(0).toUpperCase())+'</span>';
 return '<div class=""item-confirm-icon"">'+icon+'</div><div class=""item-confirm-copy""><b>'+
  esc(item.Name||'Unknown item')+' &times;'+esc(item.Quantity||0)+'</b><span>'+esc(item.WorldName||('World '+item.WorldId))+
  ' &middot; '+esc(shortPosition(item.Position))+' &middot; '+esc(droppedLifetime(item))+'</span></div>';
}
function itemClearBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('itemClearModal'))closeItemClearConfirm();
}
function closeItemClearConfirm(){
 document.getElementById('itemClearModal').className='hotfix-modal item-clear-modal';
 pendingDroppedItem=null;pendingDroppedEntityId=0;pendingDroppedMode='';
}
function confirmDroppedItemClear(){
 if(operationBusy)return;
 var targetId=pendingDroppedEntityId,targetItem=pendingDroppedItem,mode=pendingDroppedMode;
 var expiredOnly=mode==='expired';
 document.getElementById('itemClearModal').className='hotfix-modal item-clear-modal';
 pendingDroppedItem=null;pendingDroppedEntityId=0;pendingDroppedMode='';
 if(!ensureGameClosed())return;
 busy(true,targetId?'REMOVING ONE LOOSE PICKUP':(expiredOnly?'CLEARING EXPIRED PICKUPS':'CLEARING LOOSE WORLD ITEMS'),
  'Creating and verifying a safety backup before the database transaction.');
 window.setTimeout(function(){
  if(!ensureGameClosed()){busy(false);return;}
  var data=expiredOnly?
   parseResult(window.external.ClearExpiredDroppedItems(currentPath)):
   parseResult(window.external.ClearDroppedItems(currentPath,targetId));
  busy(false);
  if(data.Cancelled)return;
  if(!data.Success){
   renderAnalysis(lastAnalysis);
   document.getElementById('result').insertAdjacentHTML('afterbegin','<div class=""banner banner-error""><b>ITEM REMOVAL STOPPED.</b> '+
    esc(data.Error||'The loose-item repair did not complete.')+'</div>');
   updateScrollBar();return;
  }
  lastBackupPath=data.BackupPath||'';
  clearPerformanceState(false);
  lastAnalysis=data.After;
  renderAnalysis(lastAnalysis);
  var removedName=targetItem?(targetItem.Name||'Loose pickup'):'Loose world items';
  var message=targetId?
   esc(removedName)+' was removed after full database verification.':
   esc(data.ItemsRemoved)+(expiredOnly?' expired':'')+' stack(s), containing '+esc(data.QuantityRemoved)+
   ' total item(s), were removed and verified.';
  var html='<div class=""banner banner-good""><b>LOOSE ITEM CLEANUP COMPLETE.</b> '+message+'</div>';
  if(lastBackupPath)html+='<div class=""success-box"" style=""margin-bottom:10px""><div class=""backup-label"">VERIFIED SAFETY BACKUP</div>'+
   '<div class=""path"">'+esc(lastBackupPath)+'</div><div style=""margin-top:12px""><button class=""btn"" onclick=""openBackup()"">SHOW BACKUP IN FOLDER</button></div></div>';
  document.getElementById('result').insertAdjacentHTML('afterbegin',html);
  updateScrollBar();
 },busyLeadDelay());
}
function clearRaids(){
 if(!lastAnalysis||!lastAnalysis.CanClear)return;
 busy(true,'RELEASING CROPS & CLEARING RAIDS','Creating a verified backup, releasing registered crop growth, and removing stored raid state.');
 window.setTimeout(function(){
  var data=parseResult(window.external.ClearRaids(currentPath));busy(false);
  if(data.Cancelled)return;
  if(!data.Success){showError(data.Error||'The repair did not complete.');return;}
  lastBackupPath=data.BackupPath||'';
  clearPerformanceState(false);
  lastAnalysis=data.After;
  renderAnalysis(lastAnalysis);
  var cropMessage=Number(data.CropsReleased||0)+' crop'+(Number(data.CropsReleased||0)===1?' was':'s were')+' released';
  if(Number(data.CropsAlreadySafe||0)>0)cropMessage+='; '+Number(data.CropsAlreadySafe)+' already safe';
  if(Number(data.MissingCropReferences||0)>0)cropMessage+='; '+Number(data.MissingCropReferences)+' stale reference'+(Number(data.MissingCropReferences)===1?'':'s')+' skipped';
  document.getElementById('result').insertAdjacentHTML('afterbegin',
   '<div class=""banner banner-good""><b>RAIDS RESOLVED SAFELY.</b> '+esc(cropMessage)+'. The repaired save passed its final integrity check.</div>'+
   '<div class=""success-box"" style=""margin-bottom:10px""><div class=""backup-label"">VERIFIED SAFETY BACKUP</div><div class=""path"">'+esc(lastBackupPath)+'</div>'+
   '<div style=""margin-top:12px""><button class=""btn"" onclick=""openBackup()"">SHOW BACKUP IN FOLDER</button></div></div>');
  updateScrollBar();
 },busyLeadDelay());
}
function repairOrphanedCrops(){
 if(!lastAnalysis||!lastAnalysis.CanRepairOrphanedCrops)return;
 var expected=Number(lastAnalysis.OrphanedRaidCropCount||0);
 busy(true,'RELEASING ORPHANED CROPS','Creating a verified backup and repairing only crops that are no longer linked to an active raid.');
 window.setTimeout(function(){
  var data=parseResult(window.external.RepairOrphanedRaidCrops(currentPath,expected));busy(false);
  if(data.Cancelled)return;
  if(!data.Success){showError(data.Error||'The orphaned crop repair did not complete.');return;}
  lastBackupPath=data.BackupPath||'';
  clearPerformanceState(false);
  lastAnalysis=data.After;
  renderAnalysis(lastAnalysis);
  document.getElementById('result').insertAdjacentHTML('afterbegin',
   '<div class=""banner banner-good""><b>CROP GROWTH RELEASED.</b> '+esc(Number(data.CropsReleased||0))+
   ' orphaned crop'+(Number(data.CropsReleased||0)===1?' is':'s are')+' no longer waiting for a deleted raid.</div>'+
   '<div class=""success-box"" style=""margin-bottom:10px""><div class=""backup-label"">VERIFIED SAFETY BACKUP</div><div class=""path"">'+esc(lastBackupPath)+'</div>'+
   '<div style=""margin-top:12px""><button class=""btn"" onclick=""openBackup()"">SHOW BACKUP IN FOLDER</button></div></div>');
  updateScrollBar();
 },busyLeadDelay());
}
var lastGameBackupPath='';
function installRaidHotfix(){
 if(operationBusy)return;
 var modal=document.getElementById('hotfixModal');
 modal.className='hotfix-modal show';
 window.setTimeout(function(){document.getElementById('hotfixConfirmButton').focus();},30);
}
function hotfixBackdropClick(e){
 e=e||window.event;
 if((e.target||e.srcElement)===document.getElementById('hotfixModal'))closeHotfixConfirm();
}
function closeHotfixConfirm(){
 document.getElementById('hotfixModal').className='hotfix-modal';
}
function confirmHotfixInstall(){
 closeHotfixConfirm();
 busy(true,'VERIFYING GAME FILES','Checking original and previous ScrapLab states before a cumulative update.');
 window.setTimeout(function(){
  var data=parseResult(window.external.InstallRaidHotfix());busy(false);
  if(data.Cancelled)return;
  if(!data.Success){
   renderAnalysis(lastAnalysis);
   document.getElementById('result').insertAdjacentHTML('afterbegin','<div class=""banner banner-error"">'+esc(data.Error||'The game hotfix was not installed.')+'</div>');
   updateScrollBar();return;
  }
  lastGameBackupPath=data.BackupPath||'';
  renderAnalysis(lastAnalysis);
  var title=data.AlreadyPatched?'GAME HOTFIX ALREADY UP TO DATE.':'GAME HOTFIX UPDATED.';
   var detail=data.AlreadyPatched?'The latest verified cumulative 1.0.2 patch is already present.':'Updated '+esc(data.FilesPatched)+' verified game scripts for Scrap Mechanic '+esc(data.GameVersion)+'. Game cache reset; the next normal launch may take a little longer.';
  var html='<div class=""banner banner-good""><b>'+title+'</b> '+detail+'</div>';
  if(lastGameBackupPath)html+='<div class=""success-box"" style=""margin-bottom:10px""><div class=""backup-label"">VERIFIED GAME-SCRIPT BACKUP</div>'+
   '<div class=""path"">'+esc(lastGameBackupPath)+'</div><div style=""margin-top:12px""><button class=""btn"" onclick=""openGameBackup()"">SHOW GAME BACKUP</button></div></div>';
  document.getElementById('result').insertAdjacentHTML('afterbegin',html);
  updateScrollBar();
 },busyLeadDelay());
}
function openBackup(){if(lastBackupPath)window.external.OpenFolder(lastBackupPath);}
function openGameBackup(){if(lastGameBackupPath)window.external.OpenFolder(lastGameBackupPath);}
</script>
</body>
</html>";
    }
}
