using System;
using System.Runtime.CompilerServices;

namespace EffectViewer.TodLib.Common
{
    public interface IDataArrayItem
    {
        /// <summary>
        /// 如果这个Item在使用，那ID就是他的ID，否则就是他指向的下一个Item的ID
        /// </summary>
        uint Id { get; set; }

        /// <summary>
        /// 在数组中的物理位置，不考虑静态链表
        /// </summary>
        int Index { get; init; }

        void Reset();

        void Dispose();
    }

    public class DataArray<TItem, TId> where TItem : class, IDataArrayItem, new() where TId : unmanaged
    {
        private const uint DATA_ARRAY_INDEX_MASK = 0x0000FFFF;
        private const uint DATA_ARRAY_KEY_MASK = 0xFFFF0000;
        private const int DATA_ARRAY_KEY_SHIFT = 16;
        private const uint DATA_ARRAY_MAX_SIZE = 65536;
        private const uint DATA_ARRAY_KEY_FIRST = 1;

        public TItem[] mBlock;
        public uint mMaxUsedCount;
        public uint mMaxSize;
        public uint mFreeListHead;
        public uint mSize;
        public uint mNextKey;
        public string mName;

        public DataArray()
        {
            if (Unsafe.SizeOf<TId>() != 4)
            {
                throw new Exception("DataArray id size must be 4!");
            }
            mBlock = null;
            mMaxUsedCount = 0U;
            mMaxSize = 0U;
            mFreeListHead = 0U;
            mSize = 0U;
            mNextKey = 1U;
            mName = null;
        }

        ~DataArray()
        {
            DataArrayDispose();
        }

        public void DataArrayInitialize(uint theMaxSize, string theName)
        {
            Debug.ASSERT(mBlock == null);
            mBlock = new TItem[theMaxSize];
            for (int i = 0; i < theMaxSize; i++)
            {
                mBlock[i] = new TItem()
                {
                    Index = i,
                };
            }
            mMaxSize = theMaxSize;
            mNextKey = 1001U;
            mName = theName;
        }

        public void DataArrayDispose()
        {
            if (mBlock != null)
            {
                DataArrayFreeAll();
                mBlock = null;
                mMaxUsedCount = 0U;
                mMaxSize = 0U;
                mFreeListHead = 0U;
                mSize = 0U;
                mName = null;
            }
        }

        public void DataArrayFree(TItem theItem)
        {
            uint aRawId = theItem.Id;
            TId aId = Unsafe.As<uint, TId>(ref aRawId);
            Debug.ASSERT(DataArrayGet(aId) == theItem);
            theItem.Dispose();
            uint anId = theItem.Id & DATA_ARRAY_INDEX_MASK;
            theItem.Id = mFreeListHead;
            mFreeListHead = anId;
            mSize--;
        }

        public void DataArrayFreeAll()
        {
            TItem aItem = null;
            while (IterateNext(ref aItem))
            {
                DataArrayFree(aItem);
            }
            mFreeListHead = 0U;
            mMaxUsedCount = 0U;
        }

        public TId DataArrayGetID(TItem theItem)
        {
            uint aRawId = theItem.Id;
            TId aId = Unsafe.As<uint, TId>(ref aRawId);
            Debug.ASSERT(DataArrayGet(aId) == theItem);
            return aId;
        }

        public bool IterateNext(ref TItem theItem)
        {
            int aItemIndex;
            if (theItem == null)
            {
                aItemIndex = 0;
            }
            else
            {
                aItemIndex = theItem.Index + 1;
            }

            while (aItemIndex < mMaxUsedCount)
            {
                if ((mBlock[aItemIndex].Id & DATA_ARRAY_KEY_MASK) != 0)
                {
                    theItem = mBlock[aItemIndex];
                    return true;
                }
                aItemIndex++;
            }
            return false;
        }

        public TItem DataArrayAlloc()
        {
            Debug.ASSERT(mSize < mMaxSize);
            Debug.ASSERT(mFreeListHead <= mMaxUsedCount);

            uint aNext = mMaxUsedCount;
            if (mFreeListHead == mMaxUsedCount)
            {
                mFreeListHead = ++mMaxUsedCount;
            }
            else
            {
                aNext = mFreeListHead;
                mFreeListHead = mBlock[mFreeListHead].Id;
            }

            TItem aNewItem = mBlock[aNext];

            aNewItem.Reset();
            aNewItem.Id = (mNextKey++ << DATA_ARRAY_KEY_SHIFT) | aNext;
            if (mNextKey == DATA_ARRAY_MAX_SIZE)
            {
                mNextKey = DATA_ARRAY_KEY_FIRST;
            }
            mSize++;
            return aNewItem;
        }

        public TItem DataArrayTryToGet(TId theId)
        {
            uint aRawId = Unsafe.As<TId, uint>(ref theId);
            if (aRawId == 0 || (aRawId & DATA_ARRAY_INDEX_MASK) >= mMaxSize)
            {
                return null;
            }

            TItem aBlock = mBlock[aRawId & DATA_ARRAY_INDEX_MASK];
            return aBlock.Id == aRawId ? aBlock : null;
        }

        public TItem DataArrayGet(TId theId)
        {
            Debug.ASSERT(DataArrayTryToGet(theId) != null);
            uint aRawId = Unsafe.As<TId, uint>(ref theId);
            return mBlock[aRawId & DATA_ARRAY_INDEX_MASK];
        }
    }
}
