using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;

namespace Gallop.Live.Cutt
{
    public class LiveTimelineMotionSequence
    {
        private ILiveTimelineMSQTarget _target; // 0x10
        private Animation[] _animationArray; // 0x18
        private int _targetIndex; // 0x20
        private LiveTimelineKeyCharaMotionSeqDataList[] _keyArray; // 0x28
        private LiveTimelineKeyCharaMotionSeqDataList _currentKey; // 0x30
        private int _indexOfPlayingKey; // 0x38
        private LiveTimelineControl _timelineControl; // 0x40
        private float _heightRate; // 0x48
        private LiveTimelineDefine.SheetIndex _currentSheetIndex; // 0x4C
        //private LivePlayableAnimator[] _playableAnimatorArray; // 0x50
        private int _prevSysTextId; // 0x58
        private const float OVERRIDE_ANIMATOR_FACIAL_WAIT = 1;
        private int _prevFrame; // 0x5C
        private float _prevFrameAnimationTime; // 0x60

        //Testing
        private Transform _tempTarget;
        private Animation _tempAnim;
        private BipedIK _sampleIk;
        private bool _resetIkBeforeSample;

        private bool _needChange;
        private LiveTimelineKeyCharaMotionData _prevKey;

        private AnimationClip _targetAnim = null;

        private bool _motionSetup = false;

        private int _curIndex = -1;
        private int _prevIndex = -1;

        public int charaIndex = -1;

        public void Initialize(Transform target, int targetIndex, int seqDataIndex, LiveTimelineControl timelineControl, List<AnimationClip> animclips = null)
        {
            if(animclips != null)
            {
                if (targetIndex < Director.instance.charaAnims.Count)
                {
                    _targetAnim = animclips[targetIndex];

                    _tempAnim = Director.instance.charaAnims[targetIndex];
                    _tempAnim.AddClip(_targetAnim, _targetAnim.name);
                }
                return;
            }

            _tempTarget = target;
            _targetIndex = targetIndex;

            charaIndex = targetIndex;

            _keyArray = timelineControl._keyArray;

            if (seqDataIndex < _keyArray.Length)
            {
                _currentKey = _keyArray[seqDataIndex];
            }

            if (targetIndex < Director.instance.charaAnims.Count)
            {
                _tempAnim = Director.instance.charaAnims[targetIndex];
                _sampleIk = _tempAnim.GetComponent<BipedIK>();
                if (_sampleIk != null)
                {
                    // SolverManager.Update otherwise restores the bind pose after our manual sample.
                    _resetIkBeforeSample = _sampleIk.fixTransforms;
                    if (_resetIkBeforeSample) _sampleIk.fixTransforms = false;
                }
            }

            if(_currentKey != null && _tempAnim != null)
            {
                foreach (var key in _currentKey.thisList)
                {
                   if(key.clip != null)
                   {
                        _tempAnim.AddClip(key.clip, key.clip.name);
                   }
                }
            }
            _tempAnim.wrapMode = WrapMode.Clamp;
            _tempAnim.enabled = false;
        }

        public void AlterUpdate(float currentTime, LiveTimelineKeyTimescaleDataList timescaleKeys)
        {
            var director = Director.instance;
            if (Director.instance.liveMode == 0)
            {
                if (_targetAnim != null)
                {
                    if (director.sliderControl.is_Touched || director.IsRecordVMD)
                    {
                        _tempAnim[_targetAnim.name].time = currentTime;
                        _tempAnim.Play(_targetAnim.name);
                        _motionSetup = false;
                    }
                    else if (!_motionSetup)
                    {
                        _tempAnim[_targetAnim.name].time = currentTime;
                        _tempAnim.Play(_targetAnim.name);
                        _motionSetup = true;
                    }
                }
                return;
            }

            if (_currentKey != null && _tempAnim != null)
            {
                LiveTimelineKeyIndex curKey = LiveTimelineControl.AlterUpdate_Key(_currentKey, currentTime);

                _curIndex = curKey.index;
                LiveTimelineKeyCharaMotionData arg = curKey.key as LiveTimelineKeyCharaMotionData;
                AnimationClip anim = arg.clip;

                double start;
                if (arg.isMotionHeadFrameAll)
                {
                    start = (double)arg.motionHeadFrame / 60;
                }
                else
                {
                    start = (double)arg.motionHeadFrameSeparetes[charaIndex] / 60;
                }

                // CalculateAnimationInterval 0x1af9640: authored zero means normal speed.
                float playSpeed = Mathf.Approximately(arg.playSpeed, 0f) ? 1f : arg.playSpeed;
                double interval = 0;
                double last_current_time = currentTime;
                if (!arg.IsTimescaleDisabled && timescaleKeys != null && timescaleKeys.Count > 0)
                {
                    var has_key = false;
                    // apply timescale keys
                    for (int i = timescaleKeys.thisList.Count - 1; i >= 0; i--)
                    {
                        
                        var scaleKey = timescaleKeys.thisList[i];
                        if (scaleKey.FrameSecond <= currentTime)
                        {
                            has_key = true;
                            if (scaleKey.FrameSecond <= arg.FrameSecond)
                            {
                                interval += (last_current_time - arg.FrameSecond) * scaleKey.Timescale * playSpeed;
                                break;
                            }
                            else
                            {
                                interval += (last_current_time - scaleKey.FrameSecond) * scaleKey.Timescale * playSpeed;
                                last_current_time = scaleKey.FrameSecond;
                            }
                        }
                    }
                    if (!has_key)
                    {
                        interval = (currentTime - arg.FrameSecond) * playSpeed;
                    }
                }
                else
                {
                    interval = (currentTime - arg.FrameSecond) * playSpeed;
                }
                float currentAnimationTime = (float)(start + interval);

                if (anim)
                {
                    if (_resetIkBeforeSample && _sampleIk != null && _sampleIk.isActiveAndEnabled)
                    {
                        _sampleIk.solvers.lookAt.FixTransforms();
                        foreach (var limb in _sampleIk.solvers.limbs) limb.FixTransforms();
                    }
                    var state = _tempAnim[anim.name];
                    state.enabled = true;
                    state.weight = 1;
                    state.time = arg.loop ? Mathf.Repeat(currentAnimationTime, state.length) : currentAnimationTime;
                    _tempAnim.Sample();
                    state.enabled = false;
                }
                _prevIndex = _curIndex;            
            }
            _prevFrameAnimationTime = currentTime;
        }
    }
}
