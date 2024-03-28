//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
namespace NCShark
{
    public sealed class Pair<T1, T2>
    {
        public T1 First;
        public T2 Second;

        public Pair(T1 pFirst, T2 pSecond) { First = pFirst; Second = pSecond; }
    }
}
