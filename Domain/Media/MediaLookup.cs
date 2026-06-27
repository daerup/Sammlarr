namespace Domain.Media;

public union MediaLookup(IMedia, IPartialMedia, UnmatchedMedia);

public union KnownMedia(IMedia, IPartialMedia);
