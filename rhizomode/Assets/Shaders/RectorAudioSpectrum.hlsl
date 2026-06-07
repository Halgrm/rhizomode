StructuredBuffer<float> _RhizomodeAudioSpectrum;
int _RhizomodeAudioSpectrumSize = 512;

float RhizomodeAudioSpectrum(in uint index)
{
    return _RhizomodeAudioSpectrum[index % (uint)_RhizomodeAudioSpectrumSize];
}
