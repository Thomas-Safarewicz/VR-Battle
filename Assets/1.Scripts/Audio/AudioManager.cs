using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _1.Scripts.Audio
{
    /// <summary>
    /// Audio manager class used to set volume, player sounds, and save audio parameters.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public AudioSource musicSource;

        [SerializeField] private AudioBank soundBank;
        [SerializeField] private AudioBank musicBank;

        // Singleton

        public static AudioManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitBanks();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Initialization
        private void InitBanks()
        {
            soundBank.Build();
            musicBank.Build();
        }

        // Public Functions

        public static void Play(string clip, Vector3? pos = null, float pitch = 1f)
        {
            if (NetworkAudio.Instance == null)
            {
                Instance.PlayLocal(clip, pos, pitch);
            }
            else if (pos != null)
            {
                NetworkAudio.Instance.RPC_PlayGlobal(clip, pos.GetValueOrDefault(), pitch);
            }
            else
            {
                NetworkAudio.Instance.RPC_PlayGlobal(clip, pitch);
            }
        }

        public static void PlayMusic(string clip)
        {
            if (NetworkAudio.Instance == null)
            {
                Instance.PlayLocal(clip);
            }
            else
            {
                NetworkAudio.Instance.RPC_PlayMusicGlobal(clip);
            }
        }

        public void PlayLocal(string clip, Vector3? position = null, float pitch = 1.0f)
        {
            if (Instance.soundBank.TryGetAudio(clip, out AudioClip audioClip))
            {
                GameObject clipObj = new(clip, typeof(AudioDestroyer));
                AudioSource src = clipObj.AddComponent<AudioSource>();
                if (position.HasValue)
                {
                    clipObj.transform.position = position.Value;
                    src.spatialBlend = 1;
                    src.volume = .3f;
                    src.rolloffMode = AudioRolloffMode.Linear;
                    src.maxDistance = 20;
                    src.dopplerLevel = 0;
                }

                src.clip = audioClip;
                src.pitch = pitch;
                src.Play();
            }
            else
            {
                Debug.LogWarning($"AudioClip '{clip}' not present in audio bank");
            }
        }

        // Play Music

        public void PlayMusicLocal(string music)
        {
            if (string.IsNullOrEmpty(music) == false)
            {
                if (Instance.musicBank.TryGetAudio(music, out AudioClip audio))
                {
                    Instance.musicSource.clip = audio;
                    Instance.musicSource.Play();
                }
                else
                {
                    Debug.LogWarning($"AudioClip '{music}' not present in music bank");
                }
            }
        }

        public static void PauseMusic()
        {
            Instance.musicSource.Pause();
        }

        public static void UnpauseMusic()
        {
            Instance.musicSource.UnPause();
        }

        public static void StopMusic()
        {
            Instance.musicSource.Stop();
            Instance.musicSource.clip = null;
        }

        public static float ToDecibels(float value)
        {
            if (value == 0) return -80;
            return Mathf.Log10(value) * 20;
        }

        public static float FromDecibels(float db)
        {
            if (db == -80) return 0;
            return Mathf.Pow(10, db / 20);
        }

        [System.Serializable]
        public class BankKVP
        {
            public string Key;
            public AudioClip Value;
        }

        [System.Serializable]
        public class AudioBank
        {
            [SerializeField] private BankKVP[] kvps;
            private readonly Dictionary<string, AudioClip> dictionary = new Dictionary<string, AudioClip>();

            public bool Validate()
            {
                if (kvps.Length == 0) return false;

                List<string> keys = new List<string>();
                foreach (var kvp in kvps)
                {
                    if (keys.Contains(kvp.Key)) return false;
                    keys.Add(kvp.Key);
                }

                return true;
            }

            public void Build()
            {
                if (Validate())
                {
                    for (int i = 0; i < kvps.Length; i++)
                    {
                        dictionary.Add(kvps[i].Key, kvps[i].Value);
                    }
                }
            }

            public bool TryGetAudio(string key, out AudioClip audio)
            {
                return dictionary.TryGetValue(key, out audio);
            }
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(AudioBank))]
        public class AudioBankDrawer : PropertyDrawer
        {
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("kvps"));
            }

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.PropertyField(position, property.FindPropertyRelative("kvps"), label, true);
                EditorGUI.EndProperty();
            }
        }

        [CustomPropertyDrawer(typeof(BankKVP))]
        public class BankKVPDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUI.BeginProperty(position, label, property);

                Rect rect1 = new Rect(position.x, position.y, position.width / 2 - 4, position.height);
                Rect rect2 = new Rect(position.center.x + 2, position.y, position.width / 2 - 4, position.height);

                EditorGUI.PropertyField(rect1, property.FindPropertyRelative("Key"), GUIContent.none);
                EditorGUI.PropertyField(rect2, property.FindPropertyRelative("Value"), GUIContent.none);

                EditorGUI.EndProperty();
            }
        }
#endif
    }
}