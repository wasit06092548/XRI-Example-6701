using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using TMPro;

/// <summary>
/// SOCKET SCORE — เกมโยนของลงช่อง/ตะกร้า นับคะแนน (Feature ใหม่ #3 ที่ต่างจาก workshop)
/// การทำงาน:
/// - วางช่องรับของ (ตะกร้า/ห่วง) ที่มี XR Socket Interactor
/// - เมื่อโยนของเข้าช่อง คะแนนจะเพิ่ม พร้อมข้อความบนจอ 3D และเสียง (ถ้าใส่)
/// - โชว์การใช้งาน Socket Interactor ของ XRI ซึ่งเป็น interaction อีกแบบที่อาจารย์สอนใน Station 4
///
/// วิธีติดตั้ง (สร้างเกมโยนบอลลงตะกร้า):
/// 1. สร้างตะกร้า: GameObject > 3D Object > Cylinder แบนๆ หรือ Cube กั้นเป็นกล่องเปิดด้านบน
///    ตั้งชื่อว่า "Basket"
/// 2. ลบ/ปิด Collider ที่เป็นสาเหตุของเด้งออก แล้ว Add Component > XR Socket Interactor
///    (Unity จะเพิ่ม Collider แบบ trigger ให้เอง ปรับเป็นกล่องใหญ่พอให้บอลหล่นเข้า)
/// 3. Add Component > Socket Score Counter (สคริปต์นี้)
/// 4. สร้างข้อความคะแนน: GameObject > 3D Object > Text - TMP ตั้งชื่อ "ScoreText"
///    วางไว้ข้างตะกร้า แล้วลากใส่ช่อง Score Text
/// 5. สร้างบอลสำหรับโยน: Sphere + Rigidbody และ XR Grab Interactable
///    แล้วทำเป็น prefab กด duplicate ไว้หลายลูก
/// 6. (ถ้าอยากให้บอลหายไปหลังได้คะแนน) ตั้ง Destroy Object After = 1 วินาที
///
/// เคล็ดลับ: ใน XR Socket Interactor ให้ตั้ง
/// - Socket Snapping Duration = 0 (ไม่ดูดแปะก็ได้)
/// - Attach Preferred Position/Rotation ตามชอบ
/// </summary>
public class SocketScoreCounter : MonoBehaviour
{
    [Header("คะแนน")]
    [Tooltip("คะแนนต่อของ 1 ชิ้น")]
    [SerializeField] int scorePerObject = 10;

    [Tooltip("รูปแบบข้อความคะแนน ({0} = ตัวเลขคะแนน)")]
    [SerializeField] string scoreFormat = "SCORE: {0}";

    [Header("จอแสดงผล")]
    [Tooltip("TextMeshPro ที่จะแสดงคะแนน")]
    [SerializeField] TMP_Text scoreText;

    [Header("เสียงและเอฟเฟกต์ (ใส่หรือไม่ใส่ก็ได้)")]
    [Tooltip("เสียงตอนได้คะแนน")]
    [SerializeField] AudioClip scoreSound;

    [Tooltip("ความดังเสียง")]
    [SerializeField] float soundVolume = 0.8f;

    [Tooltip("ทำลายวัตถุหลังเข้าช่องกี่วินาที (0 = คงไว้ในตะกร้า)")]
    [SerializeField] float destroyObjectAfter = 0f;

    XRSocketInteractor m_Socket;
    int m_Score;

    void OnEnable()
    {
        m_Socket = GetComponent<XRSocketInteractor>();
        if (m_Socket == null)
        {
            Debug.LogWarning($"[SocketScoreCounter] {name} ยังไม่มี XR Socket Interactor — ใส่ก่อนถึงจะนับคะแนนได้", this);
            enabled = false;
            return;
        }

        m_Socket.selectEntered.AddListener(OnObjectEntered);
        UpdateScoreText();
    }

    void OnDisable()
    {
        if (m_Socket != null)
            m_Socket.selectEntered.RemoveListener(OnObjectEntered);
    }

    // ---------- มีของเข้าช่อง ----------

    void OnObjectEntered(SelectEnterEventArgs args)
    {
        m_Score += scorePerObject;
        UpdateScoreText();

        if (scoreSound != null)
            AudioSource.PlayClipAtPoint(scoreSound, transform.position, soundVolume);

        // ทำลายวัตถุที่เข้าช่อง (ถ้าตั้งค่าไว้) กันตะกร้าเต็ม
        if (destroyObjectAfter > 0f)
        {
            var enteredObject = args.interactableObject.transform.gameObject;
            Destroy(enteredObject, destroyObjectAfter);
        }
    }

    void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = string.Format(scoreFormat, m_Score);
    }

    // ให้เรียกจากปุ่ม Reset ในฉากได้ (ผูกกับ UnityEvent ของปุ่ม UI ก็ได้)
    public void ResetScore()
    {
        m_Score = 0;
        UpdateScoreText();
    }
}
