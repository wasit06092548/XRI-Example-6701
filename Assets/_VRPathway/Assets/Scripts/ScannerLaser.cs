using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// SCANNER — เครื่องสแกนวัตถุ (ตามโจทย์การบ้าน XRI Example)
/// การทำงาน:
/// - เมื่อ "จับ" เครื่องสแกนเนอร์ หน้าจอจะเปิด (Animator parameter "Opened")
///   และเลเซอร์จะส่องออกมา
/// - เลเซอร์ยิง Raycast ไปข้างหน้า ถ้าชนวัตถุ เส้นจะหดมาจบที่ตัววัตถุ (เหมือน tutorial)
/// - หน้าจอแสดง "ชื่อวัตถุ" (NameText) และ "ระยะห่าง" (PositionText)
/// - ปล่อยมือ = หน้าจอพับปิด เลเซอร์ดับ
///
/// วิธีติดตั้ง (ใส่บน prefab "Scanner"):
/// 1. เลือก GameObject ชื่อ Scanner (ตัว root ที่มี Rigidbody + MeshCollider)
/// 2. Add Component > XR Grab Interactable
/// 3. Add Component > Scanner Laser (สคริปต์นี้)
/// 4. ลาก child ใน prefab มาใส่ในช่อง:
///    - Laser Line        = object ชื่อ "Laser" (มี Line Renderer)
///    - Screen Animator   = ตัว Scanner root เอง (มี Animator)
///    - Name Text         = object ชื่อ "NameText" (TextMeshPro)
///    - Position Text     = object ชื่อ "PositionText" (TextMeshPro)
///    - Laser Origin      = object ชื่อ "AttachPoint" (หรือตัว Scanner เอง)
/// 5. ตั้ง Interaction Layer Mask ของ XR Grab Interactable = Default (หรือ "Grab")
/// </summary>
public class ScannerLaser : MonoBehaviour
{
    [Header("เลเซอร์")]
    [Tooltip("จุดที่ยิง ray ออกไป (แกน Z สีน้ำเงินชี้ไปทางไหน เลเซอร์จะไปทางนั้น)")]
    [SerializeField] Transform laserOrigin;

    [Tooltip("เส้นเลเซอร์ — ใน prefab คือ object ชื่อ Laser")]
    [SerializeField] LineRenderer laserLine;

    [Tooltip("ระยะสแกนสูงสุด (เมตร)")]
    [SerializeField] float maxScanDistance = 15f;

    [Tooltip("Layer ของวัตถุที่สแกนได้")]
    [SerializeField] LayerMask scanLayers = ~0;

    [Header("หน้าจอเครื่องสแกน")]
    [Tooltip("Animator ของหน้าจอ (ใช้ parameter ชื่อ Opened)")]
    [SerializeField] Animator screenAnimator;

    [SerializeField] string openParameter = "Opened";

    [Tooltip("ข้อความแสดงชื่อวัตถุที่สแกนเจอ")]
    [SerializeField] TMP_Text nameText;

    [Tooltip("ข้อความแสดงระยะห่างจากวัตถุ")]
    [SerializeField] TMP_Text positionText;

    [Header("สีเลเซอร์")]
    [SerializeField] Color idleColor = new Color(1f, 0.25f, 0.25f, 0.6f);
    [SerializeField] Color foundColor = new Color(0.25f, 1f, 0.4f, 0.9f);

    IXRSelectInteractable m_Interactable;
    bool m_IsHeld;
    RaycastHit m_Hit;

    void Awake()
    {
        if (laserOrigin == null)
            laserOrigin = transform;

        if (laserLine != null)
        {
            laserLine.useWorldSpace = true;
            laserLine.positionCount = 2;
            laserLine.enabled = false;
        }
    }

    void OnEnable()
    {
        // หา Interactable ที่อยู่บน object เดียวกัน (ต้องมี XR Grab Interactable)
        m_Interactable = GetComponent<IXRSelectInteractable>();
        if (m_Interactable == null || m_Interactable as Object == null)
        {
            Debug.LogWarning($"[ScannerLaser] {name} ยังไม่มี XR Grab Interactable — ใส่ก่อนถึงจะสแกนได้", this);
            enabled = false;
            return;
        }

        m_Interactable.selectEntered.AddListener(OnGrabbed);
        m_Interactable.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        if (m_Interactable as Object != null)
        {
            m_Interactable.selectEntered.RemoveListener(OnGrabbed);
            m_Interactable.selectExited.RemoveListener(OnReleased);
        }
    }

    // ---------- จับ / ปล่อย ----------

    void OnGrabbed(SelectEnterEventArgs args)
    {
        m_IsHeld = true;

        if (laserLine != null)
            laserLine.enabled = true;

        if (screenAnimator != null)
            screenAnimator.SetBool(openParameter, true);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        m_IsHeld = false;

        if (laserLine != null)
            laserLine.enabled = false;

        if (screenAnimator != null)
            screenAnimator.SetBool(openParameter, false);

        if (nameText != null)
            nameText.text = "--";
        if (positionText != null)
            positionText.text = "--";
    }

    // ---------- สแกนทุกเฟรม (ตามที่ workshop สอน: ScanForObjects) ----------

    void Update()
    {
        if (!m_IsHeld)
            return;

        ScanForObjects();
    }

    void ScanForObjects()
    {
        Vector3 origin = laserOrigin.position;
        Vector3 direction = laserOrigin.forward;

        bool hasHit = Physics.Raycast(
            origin, direction, out m_Hit, maxScanDistance, scanLayers,
            QueryTriggerInteraction.Ignore);

        float distance = hasHit ? m_Hit.distance : maxScanDistance;
        Vector3 endPoint = hasHit ? m_Hit.point : origin + direction * maxScanDistance;

        // วาด ray ใน Scene view ให้เห็นตอน debug (เหมือนใน tutorial)
        Debug.DrawRay(origin, direction * distance, hasHit ? Color.green : Color.red);

        // เลเซอร์ยืด-หดตามระยะที่ชน (Dynamically change the size of the laser)
        if (laserLine != null)
        {
            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, endPoint);
            laserLine.startColor = hasHit ? foundColor : idleColor;
            laserLine.endColor = hasHit ? foundColor : idleColor;
        }

        // แสดงผลบนหน้าจอเครื่องสแกน
        if (nameText != null)
            nameText.text = hasHit ? CleanName(m_Hit.collider.name) : "NO OBJECT";

        if (positionText != null)
            positionText.text = hasHit
                ? $"{m_Hit.distance:0.00} m"
                : $"{maxScanDistance:0} m";
    }

    string CleanName(string raw)
    {
        // ตัด "(Clone)" ที่ Unity ต่อท้าย object ที่ spawn มาให้อ่านง่ายขึ้น
        string n = raw.Replace("(Clone)", "").Trim();
        return n;
    }
}
