﻿using UnityEngine;

namespace Devil.AI
{
    [BTComposite(Title = "计划任务 (S)", Detail = "每轮行为树循环从子任务中\n（依次/随机）执行一个任务",
        IconPath = "Assets/DevilFramework/Gizmos/AI Icons/sequence.png", HotKey = KeyCode.S)]
    public class BTScheduled : BTControllerAsset
    {
        public enum ELoopMode
        {
            Repeat,
            PingPong,
            Random,
        }

        public ELoopMode m_LoopMode;

        public bool m_ResetWhenAbort;

        int mExecI;
        int[] mExecIndex;
        bool mRandom;
        bool mRevert;

        public override void OnPrepare(BehaviourTreeRunner.AssetBinder asset, BTNode node)
        {
            base.OnPrepare(asset, node);
            Init();
        }

        void Init()
        {
            mRandom = m_LoopMode == ELoopMode.Random && mChildren.Length > 0;
            if (mRandom)
            {
                if (mExecIndex == null || mExecIndex.Length != mChildren.Length)
                    mExecIndex = new int[mChildren.Length];
                for (int i = 0; i < mExecIndex.Length; i++)
                {
                    mExecIndex[i] = i;
                }
            }
            mExecI = 0;
        }

        public override IBTNode GetNextChildTask()
        {
            var i = mRandom ? AIUtility.GetRandomIndex(mExecIndex, mExecI) : mExecI;
            if (mRevert)
                return mChildren[mChildren.Length - 1 - i];
            else
                return mChildren[i];
        }

        public override EBTState OnAbort()
        {
            if (m_ResetWhenAbort)
            {
                mExecI = 0;
                mRevert = false;
            }
            return EBTState.failed;
        }

        public override EBTState OnReturn(EBTState state)
        {
            if (mChildren.Length > 0)
            {
                mExecI = (mExecI + 1) % mChildren.Length;
                if (mExecI == 0 && m_LoopMode == ELoopMode.PingPong && mChildren.Length > 2)
                {
                    mRevert = !mRevert;
                    mExecI++;
                }
            }
            return state;
        }

        public override EBTState OnStart()
        {
#if UNITY_EDITOR
            if (mRandom ^ (m_LoopMode == ELoopMode.Random && mChildren.Length > 0))
                Init();
#endif
            if (mChildren.Length > 0)
            {
                return EBTState.running;
            }
            else
            {
                return EBTState.success;
            }
        }
        
    }
}