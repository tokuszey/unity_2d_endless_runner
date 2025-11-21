using UnityEngine;
using System.Linq;

public class PlayerController : MonoBehaviour
{
    private Animator animator;
    private CharacterController character;
    private Vector3 direction;

    [Header("Movement Settings")]
    public float gravity = 9.81f * 2f;
    public float jumpForce = 8f;

    [Header("Microphone Settings")]
    public float blowThreshold = 0.05f;   // Üfleme hassasiyeti
    public float minSilenceTime = 0.3f;   // Üfledikten sonra bekleme süresi
    public int sampleWindow = 128;        // Örnek sayısı (ne kadar sık ölçüm yapılacağı)
    private AudioClip micClip;
    private string micDevice;
    private float lastBlowTime;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        character = GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            micClip = Microphone.Start(micDevice, true, 1, 44100);
            Debug.Log("Mikrofon başlatıldı: " + micDevice);
        }
        else
        {
            Debug.LogWarning("Mikrofon bulunamadı!");
        }
    }

    private void OnEnable()
    {
        direction = Vector3.zero;
    }

    private void Update()
    {
        // Oyundan çıkış
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
            return;
        }

        // Yerçekimi uygula
        direction += Vector3.down * gravity * Time.deltaTime;

        if (!GameManager.Instance.isGameOver)
        {
            if (character.isGrounded)
            {
                direction = Vector3.down;
                animator.SetBool("isGrounded", true);

                // 🔸 Üflemeyi kontrol et
                if (IsBlowDetected())
                {
                    direction = Vector3.up * jumpForce;
                    animator.SetBool("isGrounded", false);
                }
            }
        }

        // Oyun bittiğinde yeniden başlat
        if (GameManager.Instance.isGameOver && Input.GetKeyDown(KeyCode.R))
        {
            GameManager.Instance.NewGame();
        }

        // Hareket ettir
        character.Move(direction * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
            GameManager.Instance.GameOver();
    }

    // 🔹 Üfleme algılama sistemi
    private bool IsBlowDetected()
    {
        if (micClip == null) return false;

        int micPos = Microphone.GetPosition(micDevice) - sampleWindow;
        if (micPos < 0) return false;

        float[] data = new float[sampleWindow];
        micClip.GetData(data, micPos);

        // RMS (ortalama enerji)
        float rms = Mathf.Sqrt(data.Average(x => x * x));

        // Peak (ani patlama) — üfleme sesi kısa ama güçlüdür
        float peak = data.Max(Mathf.Abs);

        // Dinamik eşik (normal konuşmadan ayırt etmek için)
        bool blowDetected = (peak > blowThreshold * 2f && rms > blowThreshold);

        // Son üflemeyi baz al, spam olmasın
        if (blowDetected && Time.time - lastBlowTime > minSilenceTime)
        {
            lastBlowTime = Time.time;
            Debug.Log($"💨 Üfleme algılandı! RMS={rms:F3}, Peak={peak:F3}");
            return true;
        }

        return false;
    }

    private void QuitGame()
    {
#if UNITY_STANDALONE
        Application.Quit();
#endif
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
