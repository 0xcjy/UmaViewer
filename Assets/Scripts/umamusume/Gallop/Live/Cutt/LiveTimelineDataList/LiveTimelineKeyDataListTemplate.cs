using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyDataListTemplate<T> : ILiveTimelineKeyDataList where T : LiveTimelineKey
    {
        [SerializeField]
        private LiveTimelineKeyDataListAttr _attribute;

        [SerializeField]
        private TimelineKeyPlayMode _playMode;

        [SerializeField]
        private Color _baseColor;

        [SerializeField]
        private string _description;

        public LiveTimelineKeyIndex thisTimeKeyIndex = new LiveTimelineKeyIndex();

        public LiveTimelineKeyIndex TimeKeyIndex => thisTimeKeyIndex;

        public List<T> thisList = new List<T>();

        private int _lastFindIndex = -1;

        public LiveTimelineKeyDataListAttr attribute
        {
            get => _attribute;
            set => _attribute = value;
        }

        public TimelineKeyPlayMode playMode
        {
            get => _playMode;
            set => _playMode = value;
        }

        public Color BaseColor
        {
            get => _baseColor;
            set => _baseColor = value;
        }

        public string Description
        {
            get => _description;
            set => _description = value;
        }

        public LiveTimelineKey this[int index]
        {
            get => thisList[index];
            set => thisList[index] = value as T;
        }

        public int Count => thisList != null ? thisList.Count : 0;

        public int depthCounter => 0;

        public LiveTimelineKeyDataListTemplate()
        {
            if (thisList == null)
                thisList = new List<T>();
        }

        public LiveTimelineKeyIndex FindCurrentKey(float currentTime)
        {
            if (thisList == null || thisList.Count <= 0)
                return null;

            int ret = BinarySearchKey(0, thisList.Count - 1, currentTime);
            if (ret == -1)
                return null;

            thisTimeKeyIndex.index = ret;
            thisTimeKeyIndex.key = thisList[ret];
            thisTimeKeyIndex.nextKey = ret + 1 != thisList.Count ? thisList[ret + 1] : null;
            thisTimeKeyIndex.prevKey = ret - 1 >= 0 ? thisList[ret - 1] : null;

            return thisTimeKeyIndex;
        }

        public int BinarySearchKey(int low, int high, float time)
        {
            if (thisList == null || thisList.Count == 0 || high < 0)
                return -1;

            float frame = time * 60f;
            int mid = (low + high) / 2;

            if (low == high)
                return thisList[mid].frame <= frame ? mid : -1;

            if (mid + 1 < thisList.Count && frame >= thisList[mid].frame && frame < thisList[mid + 1].frame)
                return mid;

            if (frame >= thisList[mid].frame)
                return BinarySearchKey(mid + 1, high, time);

            return BinarySearchKey(low, mid - 1, time);
        }

        public LiveTimelineKeyIndex FindCurrentKeyLinear(float currentTime)
        {
            if (thisList == null)
                return null;

            for (int i = thisList.Count - 1; i >= 0; i--)
            {
                if (currentTime >= thisList[i].FrameSecond)
                {
                    thisTimeKeyIndex.index = i;
                    thisTimeKeyIndex.key = thisList[i];
                    thisTimeKeyIndex.nextKey = i + 1 != thisList.Count ? thisList[i + 1] : null;
                    thisTimeKeyIndex.prevKey = i - 1 >= 0 ? thisList[i - 1] : null;
                    return thisTimeKeyIndex;
                }
            }

            return null;
        }

        public LiveTimelineKeyIndex UpdateCurrentKey(float currentTime)
        {
            if (thisTimeKeyIndex.nextKey != null)
            {
                if (currentTime >= thisTimeKeyIndex.nextKey.FrameSecond)
                {
                    thisTimeKeyIndex.index++;
                    thisTimeKeyIndex.key = thisList[thisTimeKeyIndex.index];
                    thisTimeKeyIndex.nextKey = thisTimeKeyIndex.index + 1 != thisList.Count ? thisList[thisTimeKeyIndex.index + 1] : null;
                    thisTimeKeyIndex.prevKey = thisTimeKeyIndex.index - 1 >= 0 ? thisList[thisTimeKeyIndex.index - 1] : null;
                }
            }

            return thisTimeKeyIndex;
        }

        public bool HasAttribute(LiveTimelineKeyDataListAttr attr)
        {
            return (attribute & attr) == attr;
        }

        public bool EnablePlayModeTimeline(TimelinePlayerMode playerMode)
        {
            switch (_playMode)
            {
                case TimelineKeyPlayMode.Always:
                    return true;

                case TimelineKeyPlayMode.LightOnly:
                    return playerMode == TimelinePlayerMode.Light;

                case TimelineKeyPlayMode.DefaultOver:
                    return playerMode != TimelinePlayerMode.Light;

                default:
                    return true;
            }
        }

        public void Insert(int index, LiveTimelineKey item)
        {
            if (thisList == null)
                thisList = new List<T>();

            thisList.Insert(index, item as T);
        }

        public void Add(LiveTimelineKey item)
        {
            if (thisList == null)
                thisList = new List<T>();

            thisList.Add(item as T);
        }

        public void Clear()
        {
            if (thisList != null)
                thisList.Clear();

            _lastFindIndex = -1;
        }

        public bool Remove(LiveTimelineKey item)
        {
            if (thisList == null)
                return false;

            return thisList.Remove(item as T);
        }

        public void RemoveAt(int index)
        {
            if (thisList == null)
                return;

            thisList.RemoveAt(index);
        }

        public IEnumerator<LiveTimelineKey> GetEnumerator()
        {
            return ToEnumerable().GetEnumerator();
        }

        public List<LiveTimelineKey> GetRange(int index, int count)
        {
            return thisList.GetRange(index, count).ConvertAll(x => (LiveTimelineKey)x);
        }

        public IEnumerable<LiveTimelineKey> ToEnumerable()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        public LiveTimelineKey At(int index)
        {
            return index >= 0 && index < Count ? thisList[index] : null;
        }

        public LiveTimelineKey[] ToArray()
        {
            if (thisList == null)
                return new LiveTimelineKey[0];

            LiveTimelineKey[] ret = new LiveTimelineKey[thisList.Count];

            for (int i = 0; i < thisList.Count; i++)
                ret[i] = thisList[i];

            return ret;
        }

        public List<LiveTimelineKey> ToList()
        {
            if (thisList == null)
                return new List<LiveTimelineKey>();

            return thisList.ConvertAll(x => (LiveTimelineKey)x);
        }

        public bool Contains(LiveTimelineKey key)
        {
            return FindIndex(key) >= 0;
        }

        public int FindIndex(LiveTimelineKey key)
        {
            BinSearch(out FindKeyResult ret, out FindKeyResult next, key.frame, 0, Count - 1, Count);
            return ret.index;
        }

        public FindKeyResult FindKeyCached(int frame, bool forceRefind)
        {
            return FindKeyCached((float)frame, forceRefind);
        }

        public FindKeyResult FindKeyCached(float frame, bool forceRefind)
        {
            FindKeyCached(frame, forceRefind, out FindKeyResult current, out FindKeyResult next);
            return current;
        }

        public void FindKeyCached(float frame, bool forceRefind, out FindKeyResult current, out FindKeyResult next)
        {
            if (forceRefind || _lastFindIndex < 0)
            {
                FindKey(out current, out next, frame);
            }
            else
            {
                FindCurrentKeyNeighbor(frame, _lastFindIndex, out current, out next);
            }

            _lastFindIndex = current.index;
        }

        public FindKeyResult FindCurrentKey(int frame)
        {
            FindKey(out FindKeyResult ret, out FindKeyResult next, frame);
            return ret;
        }

        public void FindKey(out FindKeyResult ret, out FindKeyResult next, float frame)
        {
            int count = Count;

            if (count == 0)
            {
                ret.index = -1;
                ret.key = null;
                next.index = -1;
                next.key = null;
                return;
            }

            BinSearch(out ret, out next, frame, 0, count - 1, count);
        }

        private void BinSearch(out FindKeyResult ret, out FindKeyResult next, float frame, int indexS, int indexE, int listSize)
        {
            int num = ((indexE - indexS) >> 1) + indexS;
            T val = thisList[num];

            if (num + 1 < listSize)
            {
                T val2 = thisList[num + 1];

                if (val.frame <= frame && frame < val2.frame)
                {
                    ret.key = val;
                    ret.index = num;
                    next.key = val2;
                    next.index = num + 1;
                    return;
                }

                if (frame < val.frame)
                {
                    indexE = num;

                    if (indexE > indexS)
                    {
                        BinSearch(out ret, out next, frame, indexS, indexE, listSize);
                        return;
                    }
                }
                else
                {
                    indexS = num + 1;

                    if (indexS <= indexE)
                    {
                        BinSearch(out ret, out next, frame, indexS, indexE, listSize);
                        return;
                    }
                }
            }
            else if (val.frame <= frame)
            {
                ret.key = val;
                ret.index = num;
                next.key = null;
                next.index = -1;
                return;
            }

            ret.key = null;
            ret.index = -1;
            next.key = null;
            next.index = -1;
        }

        public void FindKeyLinear(out LiveTimelineKey curKey, out LiveTimelineKey nextKey, int curFrame)
        {
            curKey = null;
            nextKey = null;

            int count = Count;

            for (int i = 0; i < count; i++)
            {
                if (thisList[i].frame > curFrame)
                {
                    nextKey = thisList[i];
                    break;
                }

                curKey = thisList[i];
            }
        }

        public FindKeyResult FindCurrentKeyNeighbor(int frame, int baseIndex)
        {
            return FindCurrentKeyNeighbor((float)frame, baseIndex);
        }

        public FindKeyResult FindCurrentKeyNeighbor(float frame, int baseIndex)
        {
            FindCurrentKeyNeighbor(frame, baseIndex, out FindKeyResult ret, out FindKeyResult next);
            return ret;
        }

        public void FindCurrentKeyNeighbor(float frame, int baseIndex, out FindKeyResult ret, out FindKeyResult next)
        {
            ret.key = null;
            ret.index = -1;
            next.key = null;
            next.index = -1;

            LiveTimelineKey liveTimelineKey;
            LiveTimelineKey liveTimelineKey2;

            int count = Count;
            int num = 0;
            bool flag = false;

            while (!flag)
            {
                flag = true;

                int num2 = baseIndex + num;
                int num3 = num2 + 1;

                if (num2 < count)
                {
                    liveTimelineKey = thisList[num2];
                    liveTimelineKey2 = num3 < count ? thisList[num3] : null;

                    if (liveTimelineKey.frame <= frame)
                    {
                        if (liveTimelineKey2 == null)
                        {
                            ret.key = liveTimelineKey;
                            ret.index = num2;
                            break;
                        }

                        if (frame < liveTimelineKey2.frame)
                        {
                            ret.key = liveTimelineKey;
                            ret.index = num2;
                            next.key = liveTimelineKey2;
                            next.index = num3;
                            break;
                        }
                    }

                    flag = false;
                }

                if (num > 0)
                {
                    num2 = baseIndex - num;
                    num3 = num2 + 1;

                    if (num2 >= 0)
                    {
                        liveTimelineKey = thisList[num2];
                        liveTimelineKey2 = num3 < count ? thisList[num3] : null;

                        if (liveTimelineKey.frame <= frame)
                        {
                            if (liveTimelineKey2 == null)
                            {
                                ret.key = liveTimelineKey;
                                ret.index = num2;
                                break;
                            }

                            if (frame < liveTimelineKey2.frame)
                            {
                                ret.key = liveTimelineKey;
                                ret.index = num2;
                                next.key = liveTimelineKey2;
                                next.index = num3;
                                break;
                            }
                        }

                        flag = false;
                    }
                }

                num++;
            }
        }
    }

    [Serializable]
    public class LiveTimelineKeyIndex
    {
        public int index = -1;
        public LiveTimelineKey prevKey = null;
        public LiveTimelineKey key = null;
        public LiveTimelineKey nextKey = null;
    }
}