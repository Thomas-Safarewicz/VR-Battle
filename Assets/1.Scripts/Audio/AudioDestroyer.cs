using UnityEngine;

namespace _1.Scripts.Audio
{
	/// <summary>
	/// Manages the destruction of AudioSources when a new sound is played.
	/// </summary>
	public class AudioDestroyer : MonoBehaviour
	{
		private AudioSource src;
		private void Awake()
		{
			src = GetComponent<AudioSource>();
		}

		private void Update()
		{
			if (src == null)
			{
				src = GetComponent<AudioSource>();
			}

			// it's possible that a playOnAwake sound will not play if too many other sounds are playing
			if (src.timeSamples == src.clip.samples || src.isPlaying == false)
			{
				Destroy(gameObject);
			}
		}
	}
}
