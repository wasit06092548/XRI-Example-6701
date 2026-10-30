using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

/// <summary>
/// BALLOON INFLATOR — เครื่องเป่าลูกโป่ง (ตาม tutorial "Balloon Inflator" ใน Unity Learn)
/// การทำงาน:
/// - เมื่อ "จับ" เครื่องเป่า ลูกโป่งจะถูก spawn ที่ปลายเครื่อง (BalloonAttach)
/// - "กดไก (Trigger)" แรงเท่าไหร่ ลูกโป่งจะขยายตามแรงกด (Lerp ระหว่าง min-max)
/// - กดไกสุด (ค่าใกล้ 1) ลูกโป่งจะหลุดลอยขึ้นฟ้า แล้วมีลูกใหม่ให้เป่าต่อ
/// - ปล่อยมือกลางคัน ลูกโป่งที่เป่าค้างไว้จะหลุดลอยทันที
///
/// วิธีติดตั้ง (ใส่บน prefab "Inflator"):
/// 1. เลือก GameObject ชื่อ Inflator (ตัว root)
/// 2. Add Component > Rigidbody  (แล้วเลิกติก Use Gravity ก็ได้ ไม่เป็นไร)
/// 3. Add Component > XR Grab Interactable
/// 4. Add Component > Balloon Inflator (สคริปต์นี้)
/// 5. ลากค่าในช่อง:
///    - Balloon Attach Point = object ชื่อ "BalloonAttach" (ใน prefab Inflator)
///    - Balloon Prefab       = prefab "Balloon" (ในโฟลเดอร์ _VRPathway/Assets/Prefabs)
/// </summary>
public class BalloonInflator : MonoBehaviour
{
    [Header("ลูกโป่ง")]
    [Tooltip("จุดที่ลูกโป่งจะงอกออกมา — ใน prefab คือ object ชื่อ BalloonAttach")]
    [SerializeField] Transform balloonAttachPoint;

    [Tooltip("Prefab ลูกโป่ง")]
    [SerializeField] Balloon balloonPrefab;

    [Header("การเป่า")]
    [Tooltip("ขนาดเล็กสุดตอนเริ่มเป่า")]
    [SerializeField] float minScale = 0.2f;

    [Tooltip("ขนาดใหญ่สุดเมื่อกดไกสุด")]
    [SerializeField] float maxScale = 4f;

    [Tooltip("ค่าไกที่ถือว่า 'กดสุด' แล้วปล่อยลูกโป่ง (0-1)")]
    [SerializeField] float releaseThreshold = 0.98f;

    IXRSelectInteractable m_Interactable;
    XRInputButtonReader m_ActivateInput;
    Balloon m_BalloonInstance;
    bool m_IsHeld;

    void OnEnable()
    {
        m_Interactable = GetComponent<IXRSelectInteractable>();
        if (m_Interactable == null || m_Interactable as Object == null)
        {
            Debug.LogWarning($"[BalloonInflator] {name} ยังไม่มี XR Grab Interactable — ใส่ก่อนถึงจะเป่าลูกโป่งได้", this);
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

        // เก็บตัวอ่านค่าไกจากคอนโทรลเลอร์ของมือที่จับ (แบบเดียวกับ InteractionAnimator ใน XRI Examples)
        var controllerInteractor = args.interactorObject as XRBaseInputInteractor;
        m_ActivateInput = controllerInteractor != null ? controllerInteractor.activateInput : null;

        SpawnBalloon();
    }

    void OnReleased(SelectExitEventArgs args)
    {
        m_IsHeld = false;
        m_ActivateInput = null;

        // ปล่อยเครื่องเป่ากลางคัน -> ลูกโป่งที่ค้างอยู่หลุดลอยไปเลย
        if (m_BalloonInstance != null)
        {
            m_BalloonInstance.Detach();
            m_BalloonInstance = null;
        }
    }

    void SpawnBalloon()
    {
        if (balloonPrefab == null)
        {
            Debug.LogWarning($"[BalloonInflator] {name} ยังไม่ได้ใส่ Balloon Prefab", this);
            return;
        }

        Transform attach = balloonAttachPoint != null ? balloonAttachPoint : transform;
        m_BalloonInstance = Instantiate(balloonPrefab, attach);
        m_BalloonInstance.transform.localPosition = Vector3.zero;
        m_BalloonInstance.transform.localRotation = Quaternion.identity;
        m_BalloonInstance.transform.localScale = Vector3.one * minScale;
    }

    // ---------- เป่าลูกโป่งทุกเฟรม ----------

    void Update()
    {
        if (!m_IsHeld || m_BalloonInstance == null)
            return;

        // อ่านค่าแรงกดไก (0 = ไม่กด, 1 = กดสุด)
        float trigger = 0f;
        if (m_ActivateInput != null)
        {
            m_ActivateInput.TryReadValue(out float value);
            trigger = value;
        }

        // ขนาดลูกโป่ง = แทรกค่า (Lerp) ระหว่างเล็กสุด-ใหญ่สุดตามแรงกด
        // เหมือนบรรทัด Mathf.Lerp(1.0f, 4.0f, activateInput) ใน tutorial
        m_BalloonInstance.transform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, trigger);

        // กดไกสุด -> ปล่อยลูกโป่งลอย แล้วเตรียมลูกใหม่
        if (trigger >= releaseThreshold)
        {
            m_BalloonInstance.Detach();
            m_BalloonInstance = null;
            SpawnBalloon();
        }
    }
}
