using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// SCANNER LASER — เครื่องสแกนวัตถุ (รุ่นแก้บั๊ก "เลเซอร์พุ่งออกจากปุ่ม")
///
/// ทำไมรุ่นเก่าเลเซอร์พุ่งออกจากปุ่ม:
/// ในฉาก ช่อง Laser Origin ถูกลากไปผูกกับ object ชื่อ "AttachPoint"
/// (จุดหลังเครื่อง ติดกับปุ่มกด ScannerTrigger) ไม่ใช่ object ชื่อ "Laser"
/// ที่อยู่หน้าเครื่อง เลเซอร์จึงยิงออกจากหลัง/ตรงปุ่มแทนที่จะยิงออกหน้าเครื่อง
///
/// รุ่นใหม่ทำงานยังไง:
/// - Auto Detect Origin (เปิดอยู่): สคริปต์จะหาจุดยิงที่ "หน้าเครื่อง" ให้เอง
///   โดยเอา transform ของ object ที่มี Line Renderer (ใน prefab คือ object ชื่อ "Laser"
///   อยู่หน้าเครื่อง) หรือ child ที่ชื่อ Laser / LaserOrigin / Muzzle / LaserPoint
///   ไม่ต้องแก้อะไรใน Inspector เลย
/// - กันเลเซอร์ยิงโดนตัวเครื่องสแกนเอง (RaycastNonAlloc + ข้าม collider
///   ที่เป็นลูกของตัวเครื่อง) เหลือแค่วัตถุจริงตรงหน้า
/// - ที่เหลือเหมือนเดิม: จับเครื่อง = หน้าจอเปิด + เลเซอร์ส่อง, ปล่อย = ปิด
///   หน้าจอแสดงชื่อวัตถุ + ระยะห่าง, เส้นเลเซอร์ยืด-หดตามระยะที่ชน
///
/// วิธีติดตั้ง: Copy ไฟล์นี้ไปทับไฟล์เดิมที่
/// Assets/_VRPathway/Assets/Scripts/ScannerLaser.cs
/// (ชื่อคลาส ชื่อไฟล์ และช่องทุกช่องเหมือนเดิม ค่าที่ผูกไว้ในฉากไม่หาย)
///
/// ตรวจสอบว่าแก้สำเร็จ: กด Play แล้วดู Console ต้องมีข้อความ
///   [ScannerLaser] ... : จุดยิงเลเซอร์ = Laser
/// แล้วเลเซอร์จะพุ่งออกจากหน้าเครื่องเหมือนใน prefab
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

    [Header("แก้บั๊กจุดยิงเลเซอร์")]
    [Tooltip("หาจุดยิงที่หน้าเครื่องให้เอง (แนะนำให้เปิด) — จะใช้ object ที่มี Line Renderer หรือ child ชื่อ Laser/LaserOrigin/Muzzle แทนค่า Laser Origin ที่ผูกผิดในฉาก")]
    [SerializeField] bool autoDetectOrigin = true;

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

    // บัฟเฟอร์เก็บผลชนหลายจุด (RaycastNonAlloc ไม่ allocate หน่วยกลาง)
    readonly RaycastHit[] m_Hits = new RaycastHit[16];

    IXRSelectInteractable m_Interactable;
    bool m_IsHeld;
    bool m_HasHit;
    RaycastHit m_Hit;

    void Awake()
    {
        if (autoDetectOrigin)
            laserOrigin = FindFrontPoint();

        if (laserOrigin == null)
            laserOrigin = transform;

        if (laserLine != null)
        {
            laserLine.useWorldSpace = true;
            laserLine.positionCount = 2;
            laserLine.enabled = false;
        }
    }

    /// <summary>
    /// หาจุดยิงเลเซอร์ที่ "หน้าเครื่อง" — แก้ปัญหาที่ฉากผูก Laser Origin
    /// ไปกับ AttachPoint (หลังเครื่อง/ตรงปุ่ม) แทนที่จะเป็นหน้าเครื่อง
    /// </summary>
    Transform FindFrontPoint()
    {
        // ทางเลือกแรก: object ที่ถือ Line Renderer อยู่ (ใน prefab คือ object ชื่อ "Laser" อยู่หน้าเครื่อง)
        if (laserLine != null)
        {
            Debug.Log($"[ScannerLaser] {name} : จุดยิงเลเซอร์ = {laserLine.transform.name} (หน้าเครื่อง)", this);
            return laserLine.transform;
        }

        // ทางเลือกรอง: หาจากชื่อ child ที่บอกว่าเป็นปากกระบอกเลเซอร์
        string[] names = { "Laser", "LaserOrigin", "Muzzle", "LaserPoint" };
        foreach (string childName in names)
        {
            Transform found = transform.Find(childName);
            if (found != null)
            {
                Debug.Log($"[ScannerLaser] {name} : จุดยิงเลเซอร์ = {found.name} (หน้าเครื่อง)", this);
                return found;
            }
        }

        // หาไม่เจอ ใช้ตัวเครื่องเอง แล้วเตือนให้รู้
        Debug.LogWarning($"[ScannerLaser] {name} : ไม่พบจุดยิงหน้าเครื่อง จึงใช้ตัวเครื่องเป็นจุดยิง (ถ้าเลเซอร์ทิศผิด ให้ลาก object หน้าเครื่องใส่ช่อง Laser Origin)", this);
        return transform;
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

        m_HasHit = ScanForNearestHit(origin, direction);

        float distance = m_HasHit ? m_Hit.distance : maxScanDistance;
        Vector3 endPoint = m_HasHit ? m_Hit.point : origin + direction * maxScanDistance;

        // วาด ray ใน Scene view ให้เห็นตอน debug (เหมือนใน tutorial)
        Debug.DrawRay(origin, direction * distance, m_HasHit ? Color.green : Color.red);

        // เลเซอร์ยืด-หดตามระยะที่ชน (Dynamically change the size of the laser)
        if (laserLine != null)
        {
            laserLine.SetPosition(0, origin);
            laserLine.SetPosition(1, endPoint);
            laserLine.startColor = m_HasHit ? foundColor : idleColor;
            laserLine.endColor = m_HasHit ? foundColor : idleColor;
        }

        // แสดงผลบนหน้าจอเครื่องสแกน
        if (nameText != null)
            nameText.text = m_HasHit ? CleanName(m_Hit.collider.name) : "NO OBJECT";

        if (positionText != null)
            positionText.text = m_HasHit
                ? $"{m_Hit.distance:0.00} m"
                : $"{maxScanDistance:0} m";
    }

    /// <summary>
    /// ยิง ray แบบ NonAlloc เก็บทุกจุดที่ชน แล้วข้าม "ตัวเครื่องสแกนเอง"
    /// (collider ที่เป็นลูกของเครื่อง เช่น ตัวเครื่อง/ปุ่มกด) เพื่อให้ได้
    /// วัตถุจริงที่อยู่ตรงหน้าเท่านั้น เลือกจุดที่ใกล้ที่สุด
    /// </summary>
    bool ScanForNearestHit(Vector3 origin, Vector3 direction)
    {
        int count = Physics.RaycastNonAlloc(
            origin, direction, m_Hits, maxScanDistance, scanLayers,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float nearest = float.MaxValue;
        int nearestIndex = -1;

        for (int i = 0; i < count; i++)
        {
            // ข้าม hit ที่โดนตัวเครื่องสแกนเอง (collider เป็นลูกของ object นี้)
            if (m_Hits[i].collider == null)
                continue;
            if (m_Hits[i].collider.transform.IsChildOf(transform))
                continue;

            if (m_Hits[i].distance < nearest)
            {
                nearest = m_Hits[i].distance;
                nearestIndex = i;
                found = true;
            }
        }

        if (found)
            m_Hit = m_Hits[nearestIndex];

        return found;
    }

    string CleanName(string raw)
    {
        // ตัด "(Clone)" ที่ Unity ต่อท้าย object ที่ spawn มาให้อ่านง่ายขึ้น
        string n = raw.Replace("(Clone)", "").Trim();
        return n;
    }
}