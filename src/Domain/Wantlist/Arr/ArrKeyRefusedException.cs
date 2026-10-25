namespace Domain.Wantlist.Arr;

public sealed class ArrKeyRefusedException(string reason) : Exception(reason);
