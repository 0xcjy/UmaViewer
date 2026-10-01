import { LoopOnce, LoopRepeat } from 'three';
import { MMDAnimationHelper } from 'three/addons/animation/MMDAnimationHelper.js';
import { MMDLoader } from 'three/addons/loaders/MMDLoader.js';
import { MMDParser } from 'three/addons/libs/mmdparser.module.js';

const parser = new MMDParser.Parser();
const animationBuilder = new MMDLoader().animationBuilder;
const shiftJis = new TextDecoder('shift_jis');

// MMDParser.parseVmd stops after the camera records. Read the optional VMD footer
// separately so its per-frame IK on/off switches are not lost (notably FK leg VMDs).
function readIkTracks(buffer, metadata, ikNames) {
  const view = new DataView(buffer);
  let offset = 50 + 4 + metadata.motionCount * 111 + 4 + metadata.morphCount * 23
    + 4 + metadata.cameraCount * 61;
  const tracks = new Map();

  function skipRecords(size) {
    if (offset === view.byteLength) return false; // Older VMDs can omit the footer.
    if (view.byteLength - offset < 4) throw new Error('VMD 附加数据不完整。');
    const count = view.getUint32(offset, true);
    offset += 4;
    if (count > Math.floor((view.byteLength - offset) / size)) {
      throw new Error('VMD 附加数据已截断。');
    }
    offset += count * size;
    return true;
  }

  if (!skipRecords(28) || !skipRecords(9) || offset === view.byteLength) return tracks;
  if (view.byteLength - offset < 4) throw new Error('VMD IK 数据不完整。');
  const frameCount = view.getUint32(offset, true);
  offset += 4;
  for (let frame = 0; frame < frameCount; frame++) {
    if (view.byteLength - offset < 9) throw new Error('VMD IK 帧已截断。');
    const time = view.getUint32(offset, true) / 30;
    const count = view.getUint32(offset + 5, true);
    offset += 9; // frame number, model display flag, IK switch count
    if (count > Math.floor((view.byteLength - offset) / 21)) {
      throw new Error('VMD IK 开关已截断。');
    }
    for (let i = 0; i < count; i++) {
      const bytes = new Uint8Array(buffer, offset, 20);
      const terminator = bytes.indexOf(0);
      const name = shiftJis.decode(bytes.subarray(0, terminator < 0 ? 20 : terminator));
      const index = ikNames.get(name);
      if (index !== undefined) {
        let keys = tracks.get(index);
        if (!keys) {
          keys = [];
          tracks.set(index, keys);
        }
        keys.push({ time, enabled: view.getUint8(offset + 20) !== 0 });
      }
      offset += 21;
    }
  }
  for (const keys of tracks.values()) keys.sort((a, b) => a.time - b.time);
  return tracks;
}

function capturePose(mesh) {
  const bones = mesh.skeleton.bones;
  const pose = new Float64Array(bones.length * 10);
  for (let i = 0; i < bones.length; i++) {
    const bone = bones[i];
    bone.position.toArray(pose, i * 10);
    bone.quaternion.toArray(pose, i * 10 + 3);
    bone.scale.toArray(pose, i * 10 + 7);
  }
  return { bones: pose, morphs: mesh.morphTargetInfluences.slice() };
}

function restorePose(mesh, snapshot) {
  const bones = mesh.skeleton.bones;
  for (let i = 0; i < bones.length; i++) {
    const bone = bones[i];
    bone.position.fromArray(snapshot.bones, i * 10);
    bone.quaternion.fromArray(snapshot.bones, i * 10 + 3);
    bone.scale.fromArray(snapshot.bones, i * 10 + 7);
  }
  const influences = mesh.morphTargetInfluences;
  for (let i = 0; i < influences.length; i++) influences[i] = snapshot.morphs[i];
  mesh.updateMatrixWorld(true);
}

function releaseMotion(helper, mixer, clip, mesh) {
  if (!helper) return;
  mixer.stopAllAction();
  mixer.uncacheClip(clip);
  mixer.uncacheRoot(mesh);
  helper.remove(mesh);
}

export class MotionPlayer {
  constructor(mesh, onChange = () => {}) {
    if (!mesh?.isSkinnedMesh || !mesh.geometry?.userData?.MMD || !mesh.skeleton) {
      throw new Error('动画播放器需要已加载的 PMX 骨骼模型。');
    }
    this.mesh = mesh;
    this.onChange = onChange;
    this._helper = null;
    this._mixer = null;
    this._action = null;
    this._clip = null;
    this._name = '';
    this._time = 0;
    this._playing = false;
    this._loop = true;
    this._speed = 1;
    this._boneTrackCount = 0;
    this._morphTrackCount = 0;
    this._disposed = false;
    this._ikTracks = new Map();
    this._iks = mesh.geometry.userData.MMD.iks || [];
    this._originalIkLinks = this._iks.map(ik => ik.links.map(link => link.enabled));
    this._ikNames = new Map(this._iks.map((ik, index) => [mesh.skeleton.bones[ik.target].name, index]));
  }

  get state() {
    return {
      name: this._name,
      duration: this._clip?.duration || 0,
      time: this._time,
      playing: this._playing,
      loop: this._loop,
      speed: this._speed,
      trackCount: this._clip?.tracks.length || 0,
      boneTrackCount: this._boneTrackCount,
      morphTrackCount: this._morphTrackCount,
    };
  }

  load(buffer, name) {
    if (this._disposed) throw new Error('动画播放器已释放，无法加载 VMD。');
    if (!(buffer instanceof ArrayBuffer)) throw new Error('请选择有效的 VMD 文件（二进制数据）。');

    let vmd;
    try {
      vmd = parser.parseVmd(buffer, true);
    } catch (error) {
      throw new Error(`VMD 格式无效或文件已损坏：${String(error.message || error)}`);
    }
    if (vmd.metadata.motionCount === 0 && vmd.metadata.morphCount === 0) {
      throw new Error(vmd.metadata.cameraCount > 0
        ? '这是仅含镜头的 VMD，不能用于角色骨骼或表情动画。'
        : 'VMD 不包含角色骨骼或表情动画。');
    }
    const bones = new Set(this.mesh.skeleton.bones.map(bone => bone.name));
    const morphs = this.mesh.morphTargetDictionary;
    if (!vmd.motions.some(motion => bones.has(motion.boneName))
      && !vmd.morphs.some(morph => Object.hasOwn(morphs, morph.morphName))) {
      throw new Error('VMD 中没有与当前 PMX 模型匹配的骨骼或表情轨道，请选择对应角色的动作。');
    }

    let ikTracks;
    try {
      ikTracks = readIkTracks(buffer, vmd.metadata, this._ikNames);
    } catch (error) {
      throw new Error(`VMD 格式无效或文件已损坏：${error.message}`);
    }

    const snapshot = capturePose(this.mesh);
    let helper, mixer, clip, action;
    let boneTrackCount = 0;
    let morphTrackCount = 0;
    try {
      // MMDLoader builds absolute bone positions from the current local pose.
      // Always build against the bind pose, not the preceding clip's last frame.
      this.mesh.pose();
      clip = animationBuilder.build(vmd, this.mesh);
      clip.name = name || '未命名动作';
      for (const track of clip.tracks) {
        if (track.name.startsWith('.bones[')) boneTrackCount++;
        else if (track.name.startsWith('.morphTargetInfluences[')) morphTrackCount++;
        for (const value of track.values) {
          if (!Number.isFinite(value)) throw new Error('VMD 关键帧包含无效数字。');
        }
      }
      if (!clip.tracks.length || !Number.isFinite(clip.duration)) {
        throw new Error('VMD 没有可播放的骨骼或表情轨道。');
      }
      for (const keys of ikTracks.values()) {
        clip.duration = Math.max(clip.duration, keys[keys.length - 1].time);
      }

      this.mesh.morphTargetInfluences.fill(0);
      helper = new MMDAnimationHelper({ sync: false, pmxAnimation: true });
      helper.enable('physics', false);
      helper.add(this.mesh, { animation: clip, physics: false });
      mixer = helper.objects.get(this.mesh).mixer;
      action = mixer.existingAction(clip);
      action.setLoop(this._loop ? LoopRepeat : LoopOnce, this._loop ? Infinity : 1);
      action.clampWhenFinished = true;
      action.paused = true;
    } catch (error) {
      if (helper?.meshes.includes(this.mesh)) {
        if (mixer) releaseMotion(helper, mixer, clip, this.mesh);
        else helper.remove(this.mesh);
      }
      restorePose(this.mesh, snapshot);
      throw new Error(`无法加载 VMD 动画：${error.message || error}`);
    }

    releaseMotion(this._helper, this._mixer, this._clip, this.mesh);
    this.mesh.pose();
    this.mesh.morphTargetInfluences.fill(0);
    // With no physics, every IK link is available unless the VMD turns it off.
    helper.enable('physics', false);
    this._helper = helper;
    this._mixer = mixer;
    this._clip = clip;
    this._action = action;
    this._ikTracks = ikTracks;
    this._name = name || '未命名动作';
    this._time = 0;
    this._playing = false;
    this._boneTrackCount = boneTrackCount;
    this._morphTrackCount = morphTrackCount;
    this._sample();
    this.onChange();
  }

  play() {
    if (!this._clip || this._playing || this._clip.duration === 0) return;
    if (this._time >= this._clip.duration) this._setTime(0);
    this._playing = true;
    this.onChange();
  }

  pause() {
    if (!this._playing) return;
    this._playing = false;
    this.onChange();
  }

  restart() {
    if (!this._clip) return;
    this._setTime(0);
    this._playing = this._clip.duration > 0;
    this.onChange();
  }

  seek(seconds) {
    if (!this._clip) return;
    if (!Number.isFinite(seconds)) throw new Error('定位时间必须是有限的秒数。');
    this._setTime(Math.max(0, Math.min(seconds, this._clip.duration)));
    this.onChange();
  }

  setLoop(loop) {
    if (this._loop === Boolean(loop)) return;
    this._loop = Boolean(loop);
    if (this._action) this._action.setLoop(this._loop ? LoopRepeat : LoopOnce, this._loop ? Infinity : 1);
    this.onChange();
  }

  setSpeed(speed) {
    if (!Number.isFinite(speed) || speed <= 0) throw new Error('播放速度必须是大于零的有限数字。');
    if (this._speed === speed) return;
    this._speed = speed;
    this.onChange();
  }

  clear() {
    if (this._disposed) return;
    releaseMotion(this._helper, this._mixer, this._clip, this.mesh);
    this._helper = this._mixer = this._clip = this._action = null;
    this._ikTracks = new Map();
    this._name = '';
    this._time = 0;
    this._playing = false;
    this._boneTrackCount = this._morphTrackCount = 0;
    for (let i = 0; i < this._iks.length; i++) {
      for (let j = 0; j < this._iks[i].links.length; j++) {
        this._iks[i].links[j].enabled = this._originalIkLinks[i][j];
      }
    }
    this.mesh.pose();
    this.mesh.morphTargetInfluences.fill(0);
    this.mesh.updateMatrixWorld(true);
    this.onChange();
  }

  update(deltaSeconds) {
    if (!this._playing || !Number.isFinite(deltaSeconds) || deltaSeconds <= 0) return;
    const duration = this._clip.duration;
    const next = this._time + deltaSeconds * this._speed;
    if (this._loop) {
      this._setTime(next % duration);
    } else if (next >= duration) {
      this._setTime(duration);
      this._playing = false;
      this.onChange();
    } else {
      this._setTime(next);
    }
  }

  dispose() {
    if (this._disposed) return;
    this.clear();
    this._disposed = true;
  }

  _setTime(seconds) {
    this._time = seconds;
    this._sample();
  }

  _sample() {
    this._applyIk(this._time);
    // The helper restores pre-IK/grant bones, resamples the mixer, then applies
    // IK/grants once. Setting the action's absolute time and updating by zero
    // also works when paused or seeking backwards, without cumulative offsets.
    this._action.time = this._time;
    this._helper.update(0);
    this.mesh.updateMatrixWorld(true);
  }

  _applyIk(time) {
    for (let i = 0; i < this._iks.length; i++) {
      const keys = this._ikTracks.get(i);
      let enabled = true;
      if (keys) {
        let low = 0, high = keys.length;
        while (low < high) {
          const middle = (low + high) >>> 1;
          if (keys[middle].time <= time + 1e-7) low = middle + 1;
          else high = middle;
        }
        if (low) enabled = keys[low - 1].enabled;
      }
      for (const link of this._iks[i].links) link.enabled = enabled;
    }
  }
}
