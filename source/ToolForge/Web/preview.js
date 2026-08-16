import * as THREE from 'three';
import { ColladaLoader } from './vendor/loaders/ColladaLoader.js';
import { TGALoader } from './vendor/loaders/TGALoader.js';
import { OrbitControls } from './vendor/controls/OrbitControls.js';
import { TransformControls } from './vendor/controls/TransformControls.js';

const canvas=document.getElementById('previewCanvas');
const viewport=document.getElementById('viewport');
const scene=new THREE.Scene();
scene.background=new THREE.Color(0x101719);
scene.fog=new THREE.FogExp2(0x101719,.035);
const camera=new THREE.PerspectiveCamera(42,1,.01,1000);
camera.position.set(2.7,1.8,3.6);
const renderer=new THREE.WebGLRenderer({canvas,antialias:true,alpha:false,powerPreference:'high-performance'});
renderer.setPixelRatio(Math.min(window.devicePixelRatio||1,2));
renderer.outputColorSpace=THREE.SRGBColorSpace;
renderer.shadowMap.enabled=true;
renderer.shadowMap.type=THREE.PCFSoftShadowMap;
const orbit=new OrbitControls(camera,canvas);orbit.enableDamping=true;orbit.dampingFactor=.09;orbit.target.set(0,1,0);
const transform=new TransformControls(camera,canvas);transform.setSpace('local');transform.setSize(.72);const transformHelper=transform.getHelper?transform.getHelper():transform;scene.add(transformHelper);
const grid=new THREE.GridHelper(20,80,0x4ec9e6,0x29434a);grid.material.transparent=true;grid.material.opacity=.28;scene.add(grid);
const axes=new THREE.AxesHelper(1.4);scene.add(axes);
scene.add(new THREE.HemisphereLight(0xbfeaff,0x251b12,2.1));
const key=new THREE.DirectionalLight(0xffe2a0,4.0);key.position.set(4,7,5);key.castShadow=true;scene.add(key);
const fill=new THREE.DirectionalLight(0x62cdec,2.2);fill.position.set(-5,3,-4);scene.add(fill);
const floor=new THREE.Mesh(new THREE.CircleGeometry(10,96),new THREE.MeshStandardMaterial({color:0x151d20,roughness:1,metalness:0}));floor.rotation.x=-Math.PI/2;floor.receiveShadow=true;scene.add(floor);

const manager=new THREE.LoadingManager();
manager.setURLModifier(resolveGameToken);
manager.addHandler(/\.tga(?:$|\?)/i,new TGALoader(manager));
const colladaLoader=new ColladaLoader(manager),tgaLoader=new TGALoader(manager);
let config=null,rigRoot=null,animationRig=null,toolPoseScene=null,toolBase=null,toolContainer=null,toolAsset=null,clayReference=null,weaponJoint=null,attachmentJoint=null,cameraJoint=null,weaponMixer=null,toolMixer=null,currentWeaponAction=null,currentToolAction=null,currentClip=null,currentToolClip=null,currentAnimation='none',animations=[],toolAnimations=[],view='fp',generation=0,playing=false;
let gridOn=true,axesOn=true,boxOn=false,wireOn=false,clayOn=false,boxHelper=null,variantColor='ffffff';
let screenOn=true;
const orbitReferenceBodyScale=.62;
let historyByView={fp:[],tp:[]},futureByView={fp:[],tp:[]},suppressTransformEvent=false,lastStatusTime=0,lastFrame=performance.now(),frameCount=0,fps=0;

function resolveGameToken(url){
 const decoded=decodeURIComponent(String(url));
 const mappings=[['$SURVIVAL_DATA/','Survival/'],['$GAME_DATA/','Data/'],['$CUSTOMIZATION_DATA/','Data/']];
 for(const m of mappings){const i=decoded.indexOf(m[0]);if(i>=0)return 'https://game.toolforge/'+encodePath(m[1]+decoded.substring(i+m[0].length))}
 return url;
}
function encodePath(path){return String(path).replace(/\\/g,'/').split('/').map(encodeURIComponent).join('/')}
function event(name,detail){window.dispatchEvent(new CustomEvent(name,{detail}))}
function status(loaded,error){event('toolforge-preview-status',{loaded:!!loaded,error:error||'',rig:rigRoot?(view==='fp'?'FIRST PERSON':'THIRD PERSON'):'OFFLINE',joint:attachmentJoint?'root_bucket_jnt':'MISSING'})}
function disposeObject(root){if(!root)return;root.traverse(o=>{if(o.geometry)o.geometry.dispose();if(o.material){const list=Array.isArray(o.material)?o.material:[o.material];list.forEach(m=>{if(m.map&&m.userData&&m.userData.toolForgeOwned)m.map.dispose();m.dispose()})}});if(root.parent)root.parent.remove(root)}
function clearCurrent(){transform.detach();if(weaponMixer)weaponMixer.stopAllAction();if(toolMixer)toolMixer.stopAllAction();weaponMixer=null;toolMixer=null;disposeObject(rigRoot);disposeObject(animationRig);if(toolPoseScene&&toolPoseScene.parent)disposeObject(toolPoseScene);if(toolBase&&!animationRig)disposeObject(toolBase);rigRoot=null;animationRig=null;toolPoseScene=null;toolBase=null;toolContainer=null;toolAsset=null;weaponJoint=null;attachmentJoint=null;cameraJoint=null;clayReference=null;currentWeaponAction=null;currentToolAction=null;currentClip=null;currentToolClip=null;currentAnimation='none';playing=false;if(boxHelper){scene.remove(boxHelper);boxHelper.geometry.dispose();boxHelper.material.dispose();boxHelper=null}}
function findJoint(root,name){let found=null;const expected=String(name).toLowerCase();root.traverse(o=>{if(!found&&String(o.name).toLowerCase()===expected)found=o});return found}
function wait(milliseconds){return new Promise(resolve=>setTimeout(resolve,milliseconds))}
async function loadCollada(url,label){try{return await colladaLoader.loadAsync(url)}catch(first){await wait(180);try{return await colladaLoader.loadAsync(url+(url.includes('?')?'&':'?')+'toolforgeRetry='+Date.now())}catch(second){let path=url;try{path=decodeURIComponent(new URL(url).pathname)}catch(ignore){}throw new Error(label+' could not be loaded from Scrap Mechanic: '+path+'\n\nConfirm the selected game folder contains this file, then reopen the project.\n\n'+(second&&second.message?second.message:String(second)))}}}
function rebindSkinning(root,animationRoot,jointNames){const boneMap=new Map();animationRoot.traverse(o=>{if(o.isBone)boneMap.set(String(o.name).toLowerCase(),o)});const names=jointNames||[],bones=names.map(name=>boneMap.get(String(name).toLowerCase()));const missing=names.filter((name,index)=>!bones[index]);if(!bones.length||missing.length)throw new Error('The Bucket animation rig is missing body joint(s): '+(missing.slice(0,5).join(', ')||'unknown'));animationRoot.updateMatrixWorld(true);let skinnedCount=0;root.traverse(o=>{if(!o.isSkinnedMesh||!o.geometry)return;const indices=o.geometry.getAttribute('skinIndex');if(!indices)throw new Error('The installed character body has no skin indices.');let maximum=0;for(let i=0;i<indices.array.length;i++)maximum=Math.max(maximum,Number(indices.array[i])||0);if(maximum>=bones.length)throw new Error('The character body references skin joint '+maximum+' but only '+bones.length+' joint names were found.');const skeleton=new THREE.Skeleton(bones);const bindMatrix=o.bindMatrix&&o.bindMatrix.clone?o.bindMatrix.clone():new THREE.Matrix4();o.bind(skeleton,bindMatrix);o.normalizeSkinWeights();o.geometry.boundingBox=null;o.geometry.boundingSphere=null;skinnedCount++});if(!skinnedCount)throw new Error('The installed character body contains no skinned mesh.')}
function neutralizeRig(root){root.traverse(o=>{if(o.isMesh||o.isSkinnedMesh){const color=o.isSkinnedMesh?0xb9c6c5:0x879594;o.material=new THREE.MeshStandardMaterial({color,roughness:.72,metalness:.05,transparent:true,opacity:.88,side:THREE.DoubleSide});o.castShadow=true;o.receiveShadow=true}})}
async function loadToolMaterial(color,textureMode){
 const stem=textureMode==='vanilla-poleplant'?'obj_plants_poleplant':'obj_plants_leafplant';
 let map=null,normal=null;try{map=await tgaLoader.loadAsync('https://game.toolforge/Data/Objects/Textures/plants/'+stem+'_dif.tga');map.colorSpace=THREE.SRGBColorSpace;map.flipY=false;normal=await tgaLoader.loadAsync('https://game.toolforge/Data/Objects/Textures/plants/'+stem+'_nor.tga');normal.flipY=false}catch(e){}
 const material=new THREE.MeshStandardMaterial({color:new THREE.Color('#'+color),map,normalMap:normal,roughness:.68,metalness:.03,side:THREE.DoubleSide});material.userData.toolForgeOwned=true;return material;
}
function createRuntimeGeometry(payload,material){
 if(!payload||!Array.isArray(payload.Positions)||!Array.isArray(payload.Normals)||!Array.isArray(payload.Texcoords))throw new Error('Tool Forge did not provide normalized runtime geometry.');
 if(!payload.Positions.length||payload.Positions.length!==payload.Normals.length||payload.Positions.length/3!==payload.Texcoords.length/2)throw new Error('The normalized runtime preview geometry is incomplete.');
 const geometry=new THREE.BufferGeometry();
 geometry.setAttribute('position',new THREE.Float32BufferAttribute(payload.Positions,3));
 geometry.setAttribute('normal',new THREE.Float32BufferAttribute(payload.Normals,3));
 geometry.setAttribute('uv',new THREE.Float32BufferAttribute(payload.Texcoords,2));
 geometry.computeBoundingBox();geometry.computeBoundingSphere();
 const mesh=new THREE.Mesh(geometry,material);mesh.name='SaplingHeldMesh';mesh.castShadow=true;mesh.receiveShadow=true;
 const group=new THREE.Group();group.name='ToolForgeRuntimeGeometry';group.add(mesh);return group;
}
async function loadAll(next){
 const ticket=++generation;clearCurrent();config=next;view=next.view||'fp';variantColor=next.color||'ffffff';
 if(!next.previewGeometry){status(false,'');return}
 if(next.previewError){status(false,next.previewError);return}
 if(!next.assets){status(false,'The installed Scrap Mechanic preview assets are unavailable.');return}
 status(false,'LOADING SCRAP MECHANIC RIG...');
 try{
   const meshUrl=view==='fp'?next.assets.FirstPersonMeshUrl:next.assets.ThirdPersonMeshUrl;
   const dae=await loadCollada(meshUrl,view==='fp'?'First-person character body':'Third-person character body');if(ticket!==generation)return;
   rigRoot=dae.scene;rigRoot.name='ScrapMechanicBody';neutralizeRig(rigRoot);
   animations=view==='fp'?(next.assets.FirstPersonAnimations||[]):(next.assets.ThirdPersonAnimations||[]);
   toolAnimations=view==='fp'?(next.assets.FirstPersonToolAnimations||[]):(next.assets.ThirdPersonToolAnimations||[]);
   if(!animations.length)throw new Error('The selected Bucket preset contains no animations.');
   if(!toolAnimations.length)throw new Error('The selected Bucket tool preset contains no animations.');
   const initialAnimation=await loadCollada(animations[0].Url,'Bucket animation rig');if(ticket!==generation)return;
   animationRig=initialAnimation.scene;animationRig.name='ScrapMechanicBucketRig';scene.add(animationRig);weaponJoint=findJoint(animationRig,'jnt_right_weapon');
   if(!weaponJoint)throw new Error('The selected Bucket animation rig did not expose jnt_right_weapon.');
   cameraJoint=findJoint(animationRig,'jnt_camera');
   if(view==='fp'&&!cameraJoint)throw new Error('The first-person Bucket rig did not expose jnt_camera.');
   const initialToolPose=await loadCollada(toolAnimations[0].Url,'Bucket tool attachment rig');if(ticket!==generation)return;
   toolPoseScene=initialToolPose.scene;attachmentJoint=findJoint(toolPoseScene,'root_bucket_jnt');
   if(!attachmentJoint)throw new Error('The Bucket tool rig did not expose root_bucket_jnt.');
   if(attachmentJoint.parent)attachmentJoint.parent.remove(attachmentJoint);weaponJoint.add(attachmentJoint);
   const jointNames=view==='fp'?(next.assets.FirstPersonJointNames||[]):(next.assets.ThirdPersonJointNames||[]);rebindSkinning(rigRoot,animationRig,jointNames);scene.add(rigRoot);
   toolBase=new THREE.Group();toolBase.name='VanillaClayBasePose';toolBase.matrixAutoUpdate=false;toolBase.matrix.set(.965810,0,-.259251,.003468,0,1,0,0,.259251,0,.965810,-.206261,0,0,0,1);attachmentJoint.add(toolBase);
   toolContainer=new THREE.Group();toolContainer.name='ToolForgeLocalAdjustment';toolBase.add(toolContainer);
   const mat=await loadToolMaterial(variantColor,next.textureMode);if(ticket!==generation)return;
   toolAsset=createRuntimeGeometry(next.previewGeometry,mat);toolContainer.add(toolAsset);
   setTransform(next.transform,false);transform.attach(toolContainer);applySnap(next.transform||{});setupBox();
   event('toolforge-animations',{animations,current:'none'});
   event('toolforge-animation-time',{name:'none',time:0,duration:0,playing:false});
   if(clayOn)await ensureClay(ticket);
   updateCameraMode();if(!screenOn||view!=='fp')focus();status(true,'');
 }catch(e){if(ticket!==generation)return;status(false,e&&e.stack?e.stack:(e&&e.message?e.message:String(e)))}
}
function setTransform(value,emit){if(!toolContainer||!value)return;suppressTransformEvent=true;toolContainer.position.set(Number(value.PositionX||0)*.01,Number(value.PositionY||0)*.01,Number(value.PositionZ||0)*.01);toolContainer.rotation.set(THREE.MathUtils.degToRad(Number(value.RotationX||0)),THREE.MathUtils.degToRad(Number(value.RotationY||0)),THREE.MathUtils.degToRad(Number(value.RotationZ||0)),'XYZ');const s=Math.max(.001,Number(value.UniformScale||1));toolContainer.scale.setScalar(s);toolContainer.updateMatrixWorld(true);applySnap(value);suppressTransformEvent=false;if(emit)emitTransform()}
function applySnap(t){transform.setTranslationSnap(Math.max(.000001,Number(t.TranslationSnap||1)*.01));transform.setRotationSnap(THREE.MathUtils.degToRad(Math.max(.01,Number(t.RotationSnap||5))));transform.setScaleSnap(Math.max(.0001,Number(t.ScaleSnap||.05)))}
function readTransform(){if(!toolContainer)return null;return{PositionX:round(toolContainer.position.x*100),PositionY:round(toolContainer.position.y*100),PositionZ:round(toolContainer.position.z*100),RotationX:round(THREE.MathUtils.radToDeg(toolContainer.rotation.x)),RotationY:round(THREE.MathUtils.radToDeg(toolContainer.rotation.y)),RotationZ:round(THREE.MathUtils.radToDeg(toolContainer.rotation.z)),UniformScale:round(toolContainer.scale.x)}}
function round(n){return Math.round(Number(n)*1000000)/1000000}
function emitTransform(){if(!suppressTransformEvent){const t=readTransform();if(t)event('toolforge-transform',t)}}
function snapshot(){const t=readTransform();if(!t)return;const history=historyByView[view];history.push(t);if(history.length>80)history.shift();futureByView[view]=[]}
transform.addEventListener('dragging-changed',e=>{orbit.enabled=!e.value&&!(screenOn&&view==='fp');if(e.value)snapshot()});transform.addEventListener('objectChange',()=>{setupBox();emitTransform()});
async function setAnimation(name,ticket,loadedData){
 if(String(name).toLowerCase()==='none'){
   if(currentWeaponAction)currentWeaponAction.stop();if(currentToolAction)currentToolAction.stop();
   if(weaponMixer){weaponMixer.stopAllAction();weaponMixer.setTime(0)}if(toolMixer){toolMixer.stopAllAction();toolMixer.setTime(0)}
   currentWeaponAction=null;currentToolAction=null;currentClip=null;currentToolClip=null;currentAnimation='none';playing=false;
   event('toolforge-animations',{animations,current:'none'});
   event('toolforge-animation-time',{name:'none',time:0,duration:0,playing:false});
   return;
 }
 const item=animations.find(a=>a.Name===name),toolItem=toolAnimations.find(a=>a.Name===name);if(!item||!animationRig)return;const activeTicket=ticket||generation;
 try{const data=loadedData||await loadCollada(item.Url,'Animation '+name);const toolData=toolItem?await loadCollada(toolItem.Url,'Tool animation '+name):null;if(activeTicket!==generation)return;let clip=data.animations&&data.animations[0];if(!clip)throw new Error('Animation '+name+' contains no playable clip.');clip=clip.clone();clip.name=name;if(!weaponMixer)weaponMixer=new THREE.AnimationMixer(animationRig);if(currentWeaponAction)currentWeaponAction.stop();currentClip=clip;currentAnimation=name;playing=true;currentWeaponAction=weaponMixer.clipAction(clip);currentWeaponAction.setLoop(item.Looping?THREE.LoopRepeat:THREE.LoopOnce,item.Looping?Infinity:1);currentWeaponAction.clampWhenFinished=!item.Looping;currentWeaponAction.reset().play();currentWeaponAction.paused=false;if(toolData&&toolData.animations&&toolData.animations[0]){currentToolClip=toolData.animations[0].clone();currentToolClip.name=name;if(!toolMixer)toolMixer=new THREE.AnimationMixer(weaponJoint);if(currentToolAction)currentToolAction.stop();currentToolAction=toolMixer.clipAction(currentToolClip);currentToolAction.setLoop(toolItem.Looping?THREE.LoopRepeat:THREE.LoopOnce,toolItem.Looping?Infinity:1);currentToolAction.clampWhenFinished=!toolItem.Looping;currentToolAction.reset().play();currentToolAction.paused=false}event('toolforge-animations',{animations,current:name})}catch(e){status(true,'Animation preview: '+(e.message||e))}
}
async function ensureClay(ticket){if(clayReference||!config||!config.assets)return clayReference;const data=await loadCollada(config.assets.ClayReferenceUrl,'Clay comparison tool');if((ticket||generation)!==generation)return null;clayReference=data.scene;clayReference.name='ClayReference';clayReference.traverse(o=>{if(o.isMesh||o.isSkinnedMesh){o.material=new THREE.MeshStandardMaterial({color:0x59d9f2,wireframe:false,transparent:true,opacity:.22,depthWrite:false,side:THREE.DoubleSide})}});weaponJoint.add(clayReference);clayReference.visible=clayOn;return clayReference}
function setupBox(){if(boxHelper){scene.remove(boxHelper);boxHelper.geometry.dispose();boxHelper.material.dispose();boxHelper=null}if(boxOn&&toolContainer){boxHelper=new THREE.BoxHelper(toolContainer,0xffc72e);scene.add(boxHelper)}}
function setVariant(color){variantColor=color||'ffffff';if(!toolAsset)return;toolAsset.traverse(o=>{if(o.isMesh&&o.material){const mats=Array.isArray(o.material)?o.material:[o.material];mats.forEach(m=>m.color&&m.color.set('#'+variantColor))}})}
async function setView(next){if(view===next||!config)return;config.view=next;await loadAll(config)}
function focus(){
 const object=toolContainer||rigRoot;if(!object)return;
 scene.updateMatrixWorld(true);
 const box=new THREE.Box3().setFromObject(object);if(box.isEmpty())return;
 const center=box.getCenter(new THREE.Vector3()),size=box.getSize(new THREE.Vector3());
 // Focus is an inspection-camera operation only. Never enlarge toolContainer:
 // its scale must remain identical to the DAE generated for Scrap Mechanic.
 const radius=Math.max(size.length()*.5,.005);
 camera.fov=42;
 const halfFov=THREE.MathUtils.degToRad(camera.fov*.5);
 const distance=Math.max(radius*1.5,(radius/Math.tan(halfFov))*1.28);
 orbit.target.copy(center);
 const direction=view==='fp'?new THREE.Vector3(.8,.35,1):new THREE.Vector3(1,.65,1);
 camera.position.copy(center).add(direction.normalize().multiplyScalar(distance));
 camera.near=Math.max(.00005,distance/2000);
 camera.far=Math.max(10,distance*200);
 orbit.minDistance=Math.max(.001,radius*.08);
 orbit.maxDistance=Math.max(20,radius*500);
 camera.updateProjectionMatrix();orbit.update();
}
function updateCameraMode(){const screen=screenOn&&view==='fp'&&cameraJoint;orbit.enabled=!screen;transformHelper.visible=!!toolContainer;grid.visible=gridOn&&!screen;axes.visible=axesOn&&!screen;floor.visible=!screen;if(rigRoot){rigRoot.scale.setScalar(screen?1:orbitReferenceBodyScale);rigRoot.updateMatrixWorld(true)}if(!screen&&toolContainer)focus()}
function syncScreenCamera(){if(!(screenOn&&view==='fp'&&cameraJoint))return;animationRig.updateMatrixWorld(true);cameraJoint.getWorldPosition(camera.position);cameraJoint.getWorldQuaternion(camera.quaternion);camera.rotateY(Math.PI);camera.fov=70;camera.near=.001;camera.far=100;camera.updateProjectionMatrix()}
function toggle(kind,on){if(kind==='grid'){gridOn=on;grid.visible=on&&!(screenOn&&view==='fp')}else if(kind==='axes'){axesOn=on;axes.visible=on&&!(screenOn&&view==='fp')}else if(kind==='box'){boxOn=on;setupBox()}else if(kind==='wire'){wireOn=on;if(toolAsset)toolAsset.traverse(o=>{if(o.isMesh){const mats=Array.isArray(o.material)?o.material:[o.material];mats.forEach(m=>m.wireframe=on)}})}else if(kind==='clay'){clayOn=on;if(clayReference)clayReference.visible=on;else if(on)ensureClay(generation).catch(e=>status(true,e.message||String(e)))}else if(kind==='screen'){screenOn=on;updateCameraMode()}}
function setTransformMode(mode){transform.setMode(mode)}
function undo(){const history=historyByView[view],future=futureByView[view];if(!history.length||!toolContainer)return;future.push(readTransform());setTransform(history.pop(),true);setupBox()}
function redo(){const history=historyByView[view],future=futureByView[view];if(!future.length||!toolContainer)return;history.push(readTransform());setTransform(future.pop(),true);setupBox()}
function resetTransform(){if(!toolContainer)return;snapshot();setTransform({PositionX:0,PositionY:0,PositionZ:0,RotationX:0,RotationY:0,RotationZ:0,UniformScale:1,TranslationSnap:config.transform.TranslationSnap,RotationSnap:config.transform.RotationSnap,ScaleSnap:config.transform.ScaleSnap},true);setupBox()}
function applyUprightPreset(){if(!toolContainer||!config)return;snapshot();const upright=config.upright||{};setTransform({PositionX:0,PositionY:0,PositionZ:0,RotationX:Number(upright.RotationX||0),RotationY:Number(upright.RotationY||0),RotationZ:Number(upright.RotationZ||0),UniformScale:1,TranslationSnap:config.transform.TranslationSnap,RotationSnap:config.transform.RotationSnap,ScaleSnap:config.transform.ScaleSnap},true);setupBox();focus()}
function togglePlayback(){playing=!playing;if(currentWeaponAction)currentWeaponAction.paused=!playing;if(currentToolAction)currentToolAction.paused=!playing}
function restartAnimation(){if(currentWeaponAction){currentWeaponAction.reset().play();currentWeaponAction.paused=!playing}if(currentToolAction){currentToolAction.reset().play();currentToolAction.paused=!playing}}
function seek(ratio){if(!currentWeaponAction||!currentClip)return;const normalized=Math.max(0,Math.min(1,ratio));currentWeaponAction.time=normalized*currentClip.duration;if(currentToolAction&&currentToolClip)currentToolAction.time=normalized*currentToolClip.duration;if(weaponMixer)weaponMixer.update(0);if(toolMixer)toolMixer.update(0)}
function debugState(){if(!toolContainer)return{};scene.updateMatrixWorld(true);camera.updateMatrixWorld(true);const box=new THREE.Box3().setFromObject(toolContainer),center=box.getCenter(new THREE.Vector3()),size=box.getSize(new THREE.Vector3()),cameraPosition=camera.position.clone(),local=center.clone().sub(cameraPosition).applyQuaternion(camera.quaternion.clone().invert()),ndc=center.clone().project(camera);return{camera:cameraPosition.toArray(),cameraQuaternion:camera.quaternion.toArray(),toolCenter:center.toArray(),toolSize:size.toArray(),toolFromCamera:local.toArray(),toolNdc:ndc.toArray(),screen:screenOn&&view==='fp'}}
function resize(){const rect=viewport.getBoundingClientRect();const w=Math.max(1,Math.floor(rect.width)),h=Math.max(1,Math.floor(rect.height));if(canvas.width!==Math.floor(w*renderer.getPixelRatio())||canvas.height!==Math.floor(h*renderer.getPixelRatio())){renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix()}}
function animate(now){requestAnimationFrame(animate);resize();const dt=Math.min(.05,(now-lastFrame)/1000);lastFrame=now;if(playing&&weaponMixer)weaponMixer.update(dt);if(playing&&toolMixer)toolMixer.update(dt);if(screenOn&&view==='fp')syncScreenCamera();else orbit.update();if(boxHelper)boxHelper.update();renderer.render(scene,camera);frameCount++;if(now-lastStatusTime>500){fps=frameCount*1000/(now-lastStatusTime||1);frameCount=0;lastStatusTime=now;event('toolforge-fps',fps);if(currentWeaponAction&&currentClip)event('toolforge-animation-time',{name:currentAnimation,time:currentWeaponAction.time,duration:currentClip.duration,playing})}}

window.toolForgePreview={load:loadAll,setTransform:v=>setTransform(v,false),setTransformMode,setVariant,setView,toggle,focus,undo,redo,resetTransform,applyUprightPreset,setAnimation,togglePlayback,restartAnimation,seek,debugState};
window.addEventListener('resize',resize);status(false,'');requestAnimationFrame(animate);event('toolforge-preview-ready',{});
