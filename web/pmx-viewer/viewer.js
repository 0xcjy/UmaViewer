import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { MMDLoader } from 'three/addons/loaders/MMDLoader.js';
import { MotionPlayer } from './motion-player.js';
const modelUrl = file => new URL(`./model/${file}`, import.meta.url).href;

const viewport = document.querySelector('#viewport');
const loading = document.querySelector('#loading');
const status = document.querySelector('#status');
const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.setClearColor(0xe1e8e8);
renderer.domElement.tabIndex = 0;
renderer.domElement.setAttribute('aria-label', '无声铃鹿三维模型，拖动旋转，滚轮缩放；可用重置视角按钮恢复');
viewport.append(renderer.domElement);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 1000);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
controls.autoRotateSpeed = 1.2;
controls.maxPolarAngle = Math.PI * 0.94;
const hemisphere = new THREE.HemisphereLight(0xffffff, 0x8c9a9a, 2.1);
scene.add(hemisphere);
const key = new THREE.DirectionalLight(0xffffff, 2.5);
key.position.set(-15, 25, 30);
scene.add(key);

let model, grid, bounds, failed = false;
let motionPlayer, modelReady = false, motionLoading = false, resumeAfterSeek = false;
const motionSources = document.querySelector('#motion-sources');
const motionControls = document.querySelector('#motion-controls');
const motionStatus = document.querySelector('#motion-status');
const motionTime = document.querySelector('#motion-time');
const motionTimeText = document.querySelector('#motion-time-text');
const motionFile = document.querySelector('#motion-file');

function formatTime(seconds) {
  const ticks = Math.round(Math.max(0, seconds) * 100);
  return `${String(Math.floor(ticks / 6000)).padStart(2, '0')}:${String(Math.floor(ticks / 100) % 60).padStart(2, '0')}.${String(ticks % 100).padStart(2, '0')}`;
}

function updateMotionTime(state) {
  motionTime.value = state.time;
  const text = `${formatTime(state.time)} / ${formatTime(state.duration)}`;
  if (motionTimeText.textContent !== text) motionTimeText.textContent = text;
  motionTime.setAttribute('aria-valuetext', text);
}

function refreshMotionControls() {
  motionSources.disabled = !modelReady || failed || motionLoading;
  const state = motionPlayer?.state;
  motionControls.disabled = !state?.name || failed || motionLoading;
  if (!state) return;
  document.querySelector('#motion-name').textContent = state.name || '尚未选择动作';
  document.querySelector('#motion-play').textContent = state.playing ? '暂停' : '播放';
  document.querySelector('#motion-loop').checked = state.loop;
  document.querySelector('#motion-speed').value = String(state.speed);
  motionTime.max = state.duration;
  updateMotionTime(state);
  motionStatus.classList.remove('error');
  motionStatus.textContent = motionLoading ? '正在读取动作…' : state.name
    ? `${state.playing ? '播放中' : state.time >= state.duration ? '播放结束' : '已暂停'} · ${state.boneTrackCount} 条骨骼轨道 / ${state.morphTrackCount} 条表情轨道`
    : '可载入待机动作，或导入本地角色 VMD。';
}

async function importMotion(readBuffer, name) {
  if (!modelReady || failed || motionLoading) return;
  motionLoading = true;
  refreshMotionControls();
  let message = '';
  try {
    const buffer = await readBuffer();
    if (!failed) motionPlayer.load(buffer, name);
  } catch (error) {
    message = `动作加载失败：${error.message || error}。原动作保持不变。`;
  } finally {
    motionLoading = false;
    refreshMotionControls();
    if (message) {
      motionStatus.textContent = message;
      motionStatus.classList.add('error');
    }
  }
}

document.querySelector('#motion-demo').addEventListener('click', () => importMotion(async () => {
  const response = await fetch(modelUrl('1002_idle.vmd'));
  if (!response.ok) throw new Error(`待机动作文件不可用（HTTP ${response.status}）`);
  return response.arrayBuffer();
}, '无声铃鹿 · 待机'));
document.querySelector('#motion-import').addEventListener('click', () => motionFile.click());
motionFile.addEventListener('change', () => {
  const file = motionFile.files[0];
  motionFile.value = '';
  if (file) importMotion(() => file.arrayBuffer(), file.name);
});
document.querySelector('#motion-play').addEventListener('click', () => {
  if (motionPlayer.state.playing) motionPlayer.pause();
  else motionPlayer.play();
});
document.querySelector('#motion-restart').addEventListener('click', () => motionPlayer.restart());
document.querySelector('#motion-clear').addEventListener('click', () => motionPlayer.clear());
document.querySelector('#motion-loop').addEventListener('change', event => motionPlayer.setLoop(event.target.checked));
document.querySelector('#motion-speed').addEventListener('change', event => motionPlayer.setSpeed(Number(event.target.value)));
motionTime.addEventListener('pointerdown', () => {
  resumeAfterSeek = motionPlayer.state.playing;
  motionPlayer.pause();
});
motionTime.addEventListener('input', () => motionPlayer.seek(Number(motionTime.value)));
function finishSeek() {
  if (resumeAfterSeek) motionPlayer.play();
  resumeAfterSeek = false;
}
motionTime.addEventListener('change', finishSeek);
motionTime.addEventListener('pointerup', finishSeek);
motionTime.addEventListener('pointercancel', finishSeek);
const failedResources = new Set();
function fail(message) {
  failed = true;
  loading.hidden = false;
  document.querySelector('.spinner').hidden = true;
  document.querySelector('#loading-title').textContent = '模型未完整加载';
  document.querySelector('#loading-detail').textContent = message;
  status.textContent = '加载失败';
  document.querySelector('#display-controls').disabled = true;
  motionPlayer?.pause();
  refreshMotionControls();
}
renderer.domElement.addEventListener('webglcontextlost', event => {
  event.preventDefault();
  fail('图形上下文已丢失。请刷新页面重新加载。');
});

function frameModel() {
  if (!bounds) return;
  const size = bounds.getSize(new THREE.Vector3());
  const center = bounds.getCenter(new THREE.Vector3());
  const halfFov = THREE.MathUtils.degToRad(camera.fov / 2);
  const distance = Math.max(size.y / 2 / Math.tan(halfFov), size.x / 2 / (Math.tan(halfFov) * camera.aspect)) * 1.25 + size.z / 2;
  camera.near = Math.max(size.y / 1000, 0.001);
  camera.far = Math.max(distance * 20, size.y * 100);
  camera.position.copy(center).add(new THREE.Vector3(0, size.y * 0.025, distance));
  camera.updateProjectionMatrix();
  controls.target.copy(center);
  controls.minDistance = size.y * 0.15;
  controls.maxDistance = distance * 5;
  controls.update();
  controls.saveState();
}

const manager = new THREE.LoadingManager();
manager.onProgress = (_url, done, total) => {
  if (!failed) document.querySelector('#loading-detail').textContent = `正在读取模型与贴图 ${done} / ${total}`;
};
manager.onError = url => {
  failedResources.add(url);
  fail('无法读取资源：' + [...failedResources].join('、') + '。请确认 PMX 及 Texture2D 文件夹完整。');
};
manager.onLoad = () => {
  if (failed || !model) return;
  loading.hidden = true;
  status.textContent = '模型与贴图已就绪';
  document.querySelector('#display-controls').disabled = false;
  modelReady = true;
  refreshMotionControls();
};
const loader = new MMDLoader(manager);
loader.load(modelUrl('1002_00.pmx'), mesh => {
  model = mesh;
  scene.add(model);
  model.updateMatrixWorld(true);
  motionPlayer = new MotionPlayer(model, refreshMotionControls);
  bounds = new THREE.Box3().setFromObject(model);
  const size = bounds.getSize(new THREE.Vector3());
  const center = bounds.getCenter(new THREE.Vector3());
  grid = new THREE.GridHelper(size.y * 3, 30, 0x9eafaa, 0xc2ceca);
  grid.position.set(center.x, bounds.min.y - size.y * 0.002, center.z);
  grid.material.transparent = true;
  grid.material.opacity = 0.55;
  scene.add(grid);
  frameModel();
  const values = {
    vertices: model.geometry.attributes.position.count,
    triangles: model.geometry.index.count / 3,
    bones: model.skeleton.bones.length,
    morphs: model.morphTargetInfluences.length,
  };
  for (const [id, value] of Object.entries(values)) {
    document.getElementById(id).textContent = value.toLocaleString('en-US');
  }
}, undefined, error => {
  console.error(error);
  fail('PMX 加载失败。请确认导出文件存在且格式有效。' + (error.message || ''));
});

document.querySelector('#rotate').addEventListener('change', event => { controls.autoRotate = event.target.checked; });
document.querySelector('#grid').addEventListener('change', event => { grid.visible = event.target.checked; });
document.querySelector('#wireframe').addEventListener('change', event => {
  for (const material of model.material) material.wireframe = event.target.checked;
});
document.querySelector('#reset').addEventListener('click', () => {
  controls.autoRotate = false;
  document.querySelector('#rotate').checked = false;
  controls.reset();
  frameModel();
});

new ResizeObserver(() => {
  const width = viewport.clientWidth;
  const height = viewport.clientHeight;
  renderer.setSize(width, height);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
  frameModel();
}).observe(viewport);

const clock = new THREE.Clock();
renderer.setAnimationLoop(() => {
  const delta = Math.min(clock.getDelta(), 0.1);
  if (document.hidden || failed) return;
  if (motionPlayer) {
    motionPlayer.update(delta);
    if (motionPlayer.state.name) updateMotionTime(motionPlayer.state);
  }
  controls.update(delta);
  renderer.render(scene, camera);
});
