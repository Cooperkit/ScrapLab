namespace RaidRescue
{
    internal static class UiHtml
    {
        public const string Content = @"<!doctype html>
<html>
<head>
<meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
<meta charset=""utf-8"" />
<title>Raid Rescue</title>
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
.window-emblem-mark{position:absolute;left:4px;top:4px;width:22px;height:22px;transform:rotate(45deg);
 background:linear-gradient(145deg,#ffd74f,#efa916);border:2px solid #191b1c;border-radius:4px;
 box-shadow:0 0 0 1px #ffd13b,0 2px 0 #070808}
.logo-letter{position:absolute;left:0;top:0;width:100%;height:100%;display:block;transform:rotate(-45deg);overflow:visible}
.logo-letter .logo-letter-highlight{fill:#fff3a2;opacity:.72}
.logo-letter .logo-letter-face{fill:#272719}
.window-title{min-width:0;color:#f3f3ee;font:11px Shentox,""Arial Black"",sans-serif;letter-spacing:1px;
 white-space:nowrap;overflow:hidden;text-overflow:ellipsis;text-shadow:0 2px #000}
.window-title span{margin-left:9px;color:#8e9495;font:9px ""Inter Medium"",""Segoe UI"",sans-serif;letter-spacing:.5px}
.window-controls{height:100%;display:flex;flex:0 0 auto}
.window-button{position:relative;width:46px;height:36px;padding:0;border:0;border-left:1px solid #101213;
 color:#c7cbcb;background:transparent;cursor:pointer;outline:none}
.window-button:hover{color:#fff;background:#414647}.window-button:active{background:#191b1c}
.window-button.close:hover{background:#c83a22}.window-button.close:active{background:#8f2518}
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
.hazard{height:7px;background:repeating-linear-gradient(135deg,#f7be22 0,#f7be22 18px,#292b2c 18px,#292b2c 36px);
 border-bottom:1px solid #070808;box-shadow:0 2px 8px rgba(0,0,0,.65);background-size:51px 51px;
 animation:hazardMove 3.8s linear infinite}
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

.busy{display:none;position:fixed;top:0;right:0;bottom:0;left:0;z-index:20;background:rgba(8,9,9,.88);align-items:center;justify-content:center}
.busy.show{display:flex}.busy-card{position:relative;width:330px;padding:22px 25px 20px;background:#292c2e;border:2px solid #ffd046;
 border-radius:8px 19px 8px 19px;box-shadow:inset 0 0 0 2px #554515,0 16px 50px #000;text-align:center}
.busy-icon{width:29px;height:29px;margin:0 auto 17px;transform:rotate(45deg);background:#ffd046;border:3px solid #1c1d1e;border-radius:4px}
.busy-icon span{display:block;transform:rotate(-45deg);font:bold 15px/23px Arial;color:#272719}
.busy-card strong{font:13px Shentox,""Arial Black"",sans-serif;letter-spacing:.5px}.busy-card p{margin:6px 0 13px;color:#aeb2b2;font-size:11px}
.loading-track{height:10px;padding:2px;background:#111314;border:1px solid #050606;border-radius:4px;overflow:hidden}
.loading-fill{height:4px;width:43%;background:linear-gradient(90deg,#f19d19,#fff17d,#f19d19);animation:scan 1.1s linear infinite}
@keyframes scan{0%{margin-left:-43%}100%{margin-left:100%}}
@keyframes hazardMove{0%{background-position:0 0}100%{background-position:51px 0}}
@keyframes panelAssemble{0%{opacity:0;transform:translateY(-8px) scaleY(.96)}100%{opacity:1;transform:translateY(0) scaleY(1)}}
@keyframes indicatorPulse{0%,72%,100%{background:#ffd046;box-shadow:2px 0 #9c6a00}82%{background:#fff3a0;box-shadow:2px 0 #9c6a00,0 0 9px #ffd046}}
@keyframes shutterOpen{0%{opacity:0;transform:scaleY(.2)}100%{opacity:1;transform:scaleY(1)}}
@keyframes buttonSweep{0%{left:-45%}100%{left:125%}}
@keyframes bannerDrop{0%{opacity:0;transform:translateY(-5px)}100%{opacity:1;transform:translateY(0)}}
@keyframes dataBoot{0%{opacity:0;transform:translateY(7px)}100%{opacity:1;transform:translateY(0)}}
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

@media(max-width:900px){
 .shell{padding-left:13px;padding-right:13px}.local{display:none}.picker{flex-wrap:wrap}.save-picker{width:100%;flex-basis:100%;margin-bottom:8px}
 .picker .save-picker+*{margin-left:0}.picker .btn+.btn{margin-left:8px}.stats{flex-wrap:wrap}.stat{flex-basis:30%}
 .mini{width:33.333%}.raid-meter-wrap{display:none}.repair-bar{align-items:flex-end}.repair-bar p{padding-right:15px}.repair-actions{flex-direction:column}.repair-actions .btn+.btn{margin:8px 0 0}
}
@media(prefers-reduced-motion:reduce){*{animation:none!important;transition:none!important}}
</style>
</head>
<body onload=""boot()"">
<div class=""window-bar"">
  <div class=""window-grip"" onmousedown=""beginWindowDrag()"">
    <div class=""window-emblem"" aria-label=""Raid Rescue""><div class=""window-emblem-mark"">
      <svg class=""logo-letter"" viewBox=""0 0 22 22"" aria-hidden=""true"">
        <path class=""logo-letter-highlight"" transform=""translate(-.25 1)"" d=""M5.5 16.8V5.2h5.8c3.1 0 5.1 1.7 5.1 4.2 0 1.7-.9 3-2.4 3.7l3 3.7h-3.4l-2.5-3.1H8.3v3.1H5.5zm2.8-5.5h2.7c1.7 0 2.6-.6 2.6-1.8s-.9-1.8-2.6-1.8H8.3v3.6z""></path>
        <path class=""logo-letter-face"" transform=""translate(-.25 0)"" d=""M5.5 16.8V5.2h5.8c3.1 0 5.1 1.7 5.1 4.2 0 1.7-.9 3-2.4 3.7l3 3.7h-3.4l-2.5-3.1H8.3v3.1H5.5zm2.8-5.5h2.7c1.7 0 2.6-.6 2.6-1.8s-.9-1.8-2.6-1.8H8.3v3.6z""></path>
      </svg>
    </div></div>
    <div class=""window-title"">RAID RESCUE <span>SCRAP MECHANIC SAVE RECOVERY</span></div>
  </div>
  <div class=""window-controls"">
    <button type=""button"" class=""window-button minimize"" title=""Minimize"" aria-label=""Minimize"" onclick=""minimizeWindow()""></button>
    <button type=""button"" class=""window-button close"" title=""Close"" aria-label=""Close"" onclick=""closeWindow()""></button>
  </div>
</div>
<div class=""app-scroll"" id=""appScroll"">
<div class=""hazard""></div>
<div class=""shell"">
  <div class=""topbar"">
    <div class=""identity"">
      <div class=""brand-mark"">
        <svg class=""logo-letter"" viewBox=""0 0 22 22"" aria-hidden=""true"">
          <path class=""logo-letter-highlight"" transform=""translate(-.25 1)"" d=""M5.5 16.8V5.2h5.8c3.1 0 5.1 1.7 5.1 4.2 0 1.7-.9 3-2.4 3.7l3 3.7h-3.4l-2.5-3.1H8.3v3.1H5.5zm2.8-5.5h2.7c1.7 0 2.6-.6 2.6-1.8s-.9-1.8-2.6-1.8H8.3v3.6z""></path>
          <path class=""logo-letter-face"" transform=""translate(-.25 0)"" d=""M5.5 16.8V5.2h5.8c3.1 0 5.1 1.7 5.1 4.2 0 1.7-.9 3-2.4 3.7l3 3.7h-3.4l-2.5-3.1H8.3v3.1H5.5zm2.8-5.5h2.7c1.7 0 2.6-.6 2.6-1.8s-.9-1.8-2.6-1.8H8.3v3.6z""></path>
        </svg>
      </div>
      <div><h1>RAID RESCUE</h1><p>SURVIVAL SAVE RECOVERY UNIT</p></div>
    </div>
    <div class=""local""><b></b>OFFLINE / LOCAL SAVE ACCESS</div>
  </div>

  <div class=""panel selector-panel"">
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

  <div class=""panel diagnostics"">
    <div class=""panel-title""><strong>RAID DIAGNOSTICS</strong><span>READ-ONLY UNTIL REPAIR IS CONFIRMED</span></div>
    <div class=""diagnostic-body"" id=""result"">
      <div class=""empty""><div class=""diamond""><span>?</span></div><h4>NO WORLD ANALYZED</h4><p>Select a survival world and run the raid diagnostic.</p></div>
    </div>
  </div>
  <div class=""footer"">BACKUP-FIRST UTILITY &middot; INVENTORY / BUILDS / QUESTS / PLAYERS ARE NEVER EDITED</div>
</div>
</div>
<div class=""scroll-track"" id=""scrollTrack""><div class=""scroll-thumb"" id=""scrollThumb""></div></div>

<div class=""hotfix-modal"" id=""hotfixModal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""hotfixTitle"" onclick=""hotfixBackdropClick(event)"">
  <div class=""hotfix-dialog"">
    <div class=""hotfix-hazard""></div>
    <div class=""hotfix-head"">
      <div class=""hotfix-alert""><span>!</span></div>
      <div class=""hotfix-title""><strong id=""hotfixTitle"">SYSTEM MODIFICATION WARNING</strong><span>CUMULATIVE SCRAP MECHANIC 1.0.2 HOTFIX</span></div>
    </div>
    <div class=""hotfix-body"">
      <p class=""hotfix-intro"">Raid Rescue is ready to install or update the temporary game hotfix.</p>
      <ul class=""hotfix-checks"">
        <li>Only supported original files or verified Raid Rescue versions are accepted.</li>
        <li>Previously installed raid fixes are preserved when new fixes are added.</li>
        <li>A checksum-verified backup is created before any script is changed.</li>
        <li>The cumulative hotfix repairs stuck raids and fertilizer growth timing.</li>
      </ul>
      <div class=""hotfix-stop"">SCRAP MECHANIC MUST BE COMPLETELY CLOSED BEFORE INSTALLATION.</div>
    </div>
    <div class=""hotfix-foot"">
      <div class=""hotfix-foot-note"">Windows may request administrator permission for games installed under Program Files.</div>
      <div class=""hotfix-buttons"">
        <button type=""button"" class=""btn"" onclick=""closeHotfixConfirm()"">CANCEL</button>
        <button type=""button"" class=""btn hotfix-confirm"" id=""hotfixConfirmButton"" onclick=""confirmHotfixInstall()""><span>!</span>INSTALL HOTFIX</button>
      </div>
    </div>
  </div>
</div>

<div class=""busy"" id=""busy""><div class=""busy-card""><div class=""busy-icon""><span>!</span></div>
  <strong id=""busyTitle"">READING WORLD DATABASE</strong><p id=""busyText"">Local operation in progress.</p>
  <div class=""loading-track""><div class=""loading-fill""></div></div>
</div></div>

<script>
var currentPath='';
var lastAnalysis=null;
var lastBackupPath='';
var saveItems=[];
var scrollDrag=false;
var scrollDragY=0;
var scrollDragTop=0;
var gameRunning=null;
var operationBusy=false;

function beginWindowDrag(){window.external.BeginDrag();}
function minimizeWindow(){window.external.Minimize();}
function closeWindow(){window.external.CloseWindow();}
function updateScrollBar(){
 var pane=document.getElementById('appScroll'),track=document.getElementById('scrollTrack'),thumb=document.getElementById('scrollThumb');
 if(!pane||!track||!thumb)return;
 var viewport=pane.clientHeight,total=pane.scrollHeight;
 if(total<=viewport+1){track.className='scroll-track';return;}
 track.className='scroll-track show';
 var usable=track.clientHeight-4;
 var thumbHeight=Math.max(38,Math.floor(usable*viewport/total));
 var travel=usable-thumbHeight;
 var maxScroll=total-viewport;
 var top=2+(maxScroll>0?Math.round(travel*pane.scrollTop/maxScroll):0);
 thumb.style.height=thumbHeight+'px';
 thumb.style.top=top+'px';
}
function setupScrollBar(){
 var pane=document.getElementById('appScroll'),track=document.getElementById('scrollTrack'),thumb=document.getElementById('scrollThumb');
 pane.onscroll=updateScrollBar;
 thumb.onmousedown=function(e){
  e=e||window.event;scrollDrag=true;scrollDragY=e.clientY;scrollDragTop=parseInt(thumb.style.top,10)||2;
  thumb.className='scroll-thumb dragging';if(e.preventDefault)e.preventDefault();return false;
 };
 track.onmousedown=function(e){
  e=e||window.event;if(e.srcElement===thumb||e.target===thumb)return;
  var rect=track.getBoundingClientRect(),ratio=(e.clientY-rect.top)/track.clientHeight;
  pane.scrollTop=Math.max(0,Math.min(pane.scrollHeight-pane.clientHeight,ratio*(pane.scrollHeight-pane.clientHeight)));
 };
 document.onmousemove=function(e){
  if(!scrollDrag)return;e=e||window.event;
  var usable=track.clientHeight-4-thumb.offsetHeight;
  var top=Math.max(2,Math.min(2+usable,scrollDragTop+e.clientY-scrollDragY));
  pane.scrollTop=usable>0?(top-2)/usable*(pane.scrollHeight-pane.clientHeight):0;
 };
 document.onmouseup=function(){if(scrollDrag){scrollDrag=false;thumb.className='scroll-thumb';}};
 window.onresize=updateScrollBar;
 updateScrollBar();
}

function esc(value){
 if(value===null||typeof value==='undefined')return '';
 return String(value).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""/g,'&quot;').replace(/'/g,'&#39;');
}
function parseResult(text){
 try{return JSON.parse(String(text));}catch(e){return {Success:false,Error:'The utility returned unreadable data: '+e.message};}
}
function busy(show,title,text){
 operationBusy=show;
 document.getElementById('busyTitle').innerText=title||'WORKING';
 document.getElementById('busyText').innerText=text||'Local operation in progress.';
 document.getElementById('busy').className=show?'busy show':'busy';
 applyGameLock(gameRunning);
}
function boot(){
 document.onclick=function(){closeSaveMenu();};
 document.onkeydown=function(e){
  e=e||window.event;
  if((e.keyCode||e.which)===27)closeHotfixConfirm();
 };
 setupScrollBar();
 refreshSaves();
 window.setInterval(pollGameProcess,1000);
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
  ?'<div class=""banner banner-error""><b>WORLD ACCESS SAFETY LOCKED.</b> Scrap Mechanic is running, so Raid Rescue will not open any save database. Close the game to unlock the controls automatically.</div>':'';
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
  String(lastAnalysis.DatabaseStatus).toLowerCase()==='ok'&&!running);
 renderAnalysis(lastAnalysis);
}
function pollGameProcess(){
 if(operationBusy)return;
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
 busy(true,autoRefresh?'GAME CLOSED — REFRESHING':'DECODING RAID STORAGE',
  autoRefresh?'Updating the save status and unlocking safe repair controls.':'Running database integrity checks and reading channel 45.');
 window.setTimeout(function(){
  if(!ensureGameClosed()){busy(false);return;}
  var data=parseResult(window.external.Analyze(currentPath));
  lastAnalysis=data;
  if(data.Success||data.GameRunning){
   gameRunning=!!data.GameRunning;
   renderGameBanner(gameRunning);
  }
  renderAnalysis(data);busy(false);
 },25);
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
  stat('WORLD SIZE',data.Size,'',true)+'</div>';
 if(data.Raids&&data.Raids.length){
  for(var i=0;i<data.Raids.length;i++)html+=raidCard(data.Raids[i]);
 }else{
  html+='<div class=""empty""><div class=""diamond""><span>&#10003;</span></div><h4>RAID STORAGE CLEAR</h4><p>No persisted raid-manager entries were found in this world.</p></div>';
 }
  html+='<div class=""repair-bar""><p><b>BACKUP-FIRST RECOVERY</b><br/>Install or update the cumulative 1.0.2 game hotfix for stuck raids and fertilizer timing, or clear the stored raids from this save.</p>'+
   '<div class=""repair-actions""><button class=""btn btn-patch"" onclick=""installRaidHotfix()"">INSTALL / UPDATE HOTFIX</button>'+
   '<button class=""btn btn-danger"" '+(data.CanClear?'':'disabled=""disabled""')+' onclick=""clearRaids()"">CLEAR ALL RAIDS</button></div></div>';
 document.getElementById('result').innerHTML=html;
 updateScrollBar();
}
function stat(label,value,cls,small){
 return '<div class=""stat""><div class=""label"">'+esc(label)+'</div><div class=""value '+(small?'small ':'')+esc(cls||'')+'"">'+esc(value)+'</div></div>';
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
  mini('THREAT VALUE',r.ThreatValue+' / '+r.MaximumThreatValue)+mini('WORLD SLOT',r.WorldSlot)+mini('CENTER',pos(r.Center))+
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
function clearRaids(){
 if(!lastAnalysis||!lastAnalysis.CanClear)return;
 busy(true,'CREATING SAFETY BACKUP','The original remains untouched until the backup passes verification.');
 window.setTimeout(function(){
  var data=parseResult(window.external.ClearRaids(currentPath));busy(false);
  if(data.Cancelled)return;
  if(!data.Success){showError(data.Error||'The repair did not complete.');return;}
  lastBackupPath=data.BackupPath||'';
  document.getElementById('result').innerHTML=
   '<div class=""banner banner-good""><b>RAID STORAGE CLEARED.</b> The repaired save passed its final integrity check.</div>'+
   '<div class=""success-box""><div class=""backup-label"">VERIFIED SAFETY BACKUP</div><div class=""path"">'+esc(lastBackupPath)+'</div>'+
   '<div style=""margin-top:12px""><button class=""btn"" onclick=""openBackup()"">SHOW BACKUP IN FOLDER</button></div></div>';
   updateScrollBar();
   lastAnalysis=data.After;
 },25);
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
 busy(true,'VERIFYING GAME FILES','Checking original and previous Raid Rescue states before a cumulative update.');
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
  var detail=data.AlreadyPatched?'The latest verified cumulative 1.0.2 patch is already present.':'Updated '+esc(data.FilesPatched)+' verified game scripts for Scrap Mechanic '+esc(data.GameVersion)+'.';
  var html='<div class=""banner banner-good""><b>'+title+'</b> '+detail+'</div>';
  if(lastGameBackupPath)html+='<div class=""success-box"" style=""margin-bottom:10px""><div class=""backup-label"">VERIFIED GAME-SCRIPT BACKUP</div>'+
   '<div class=""path"">'+esc(lastGameBackupPath)+'</div><div style=""margin-top:12px""><button class=""btn"" onclick=""openGameBackup()"">SHOW GAME BACKUP</button></div></div>';
  document.getElementById('result').insertAdjacentHTML('afterbegin',html);
  updateScrollBar();
 },25);
}
function openBackup(){if(lastBackupPath)window.external.OpenFolder(lastBackupPath);}
function openGameBackup(){if(lastGameBackupPath)window.external.OpenFolder(lastGameBackupPath);}
</script>
</body>
</html>";
    }
}
