// hooks
import useTrack from '../../hooks/useTrack';

// components
import Banner from '../Feedback/Banner';

// utils
import { pickAudioFiles } from '../../utils/filePicker';
import { attachLocalAudio } from '../../utils/localAudioStore';

/**
 * Lyra only stores metadata, so a track needs the matching file on this device before the
 * player can decode it. This notice lets the user attach it in place.
 */
const MissingMediaNotice: React.FC = () => {
  const { missingMedia, clearMissingMedia, playTrack, queue } = useTrack();

  if (!missingMedia) {
    return null;
  }

  /** Registers the chosen file and starts playback with the updated queue. */
  const attach = async (): Promise<void> => {
    const [file] = await pickAudioFiles(false);

    if (!file) {
      return;
    }

    const objectUrl = attachLocalAudio(missingMedia.localTrackId, file);
    const items = (queue && queue.length > 0 ? queue : [missingMedia]).map((track) =>
      track.localTrackId === missingMedia.localTrackId ? { ...track, mediaurl: objectUrl } : track,
    );

    const started = items.find((track) => track.id === missingMedia.id) ?? missingMedia;

    clearMissingMedia();
    playTrack(started, items);
  };

  return (
    <Banner
      kind='info'
      message={`"${missingMedia.name}" has no audio file on this device yet. Pick the file to play it - it stays local.`}
      onRetry={() => void attach()}
      onDismiss={clearMissingMedia}
    />
  );
};

export default MissingMediaNotice;
