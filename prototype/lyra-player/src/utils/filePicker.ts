/** Opens the native file picker and resolves with the chosen audio files. */
export const pickAudioFiles = async (multiple = true): Promise<File[]> => {
  const input = document.createElement('input');

  input.type = 'file';
  input.accept = 'audio/*';
  input.multiple = multiple;

  const files = await new Promise<FileList | null>((resolve) => {
    input.addEventListener('change', () => resolve(input.files), { once: true });
    input.click();
  });

  return files ? Array.from(files) : [];
};

/** Reads only audio files; ignores images or PDFs dropped onto the import zone. */
export const isAudioFile = (file: File): boolean =>
  file.type.startsWith('audio/') || /\.(mp3|m4a|aac|flac|ogg|opus|wav)$/i.test(file.name);
