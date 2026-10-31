using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

/// <summary>
/// BALLOON INFLATOR — เครื่องเป่าลูกโป่ง (รุ่นแก้บั๊ก "ปั๊มลูกโป่งไม่จำกัด")
///
/// ทำไมรุ่นเก่าปั๊มลูกโป่งออกมาไม่จำกัด:
/// โค้ดเก่าเช็ค "กดไกสุด (ค่า >= 0.98)" ทุกเฟรม — ถ้ากดค้างไว้แค่ 1 วินาที
/// โค้ดจะปล่อยลูกโป่ง + spawn ลูกใหม่ราว 60-90 ครั้ง ลูกโป่งจึงพุ่งออกมาเป็นชุด
///
/// รุ่นใหม่ทำงานยังไง (เหมือนคลิปเพื่อน):
/// - จับเครื่องเป่า = มีลูกโป่งเล็กๆ งอกที่ปลายท่อ (BalloonAttach)
/// - กดไกแรงเท่าไหร่ ลูกโป่งขยาย "ตามแรงกด" (Lerp min-max)
/// - ปล่อยไก = ลูกโป่งค่อยๆ หดกลับเล็กลง ไม่หลุด ไม่พุ่งออกมา
/// - ปล่อยมือจากเครื่องเป่ากลางคัน = ลูกโป่งที่เป่าค้างไว้หลุดลอยขึ้นฟ้า
/// - (ทางเลือก) เปิด Release When Fully Squeezed ถ้าอยากให้กดสุดแล้วปล่อยลูก
///   1 ลูกต่อการกด 1 ครั้ง — กดค้างไว้ก็ได้แค่ 1 ลูกเท่านั้น ไม่มีการปั๊มรัว
///
/// วิธีติดตั้ง: Copy ไฟล์นี้ไปทับไฟล์เดิมที่
/// Assets/_VRPathway/Assets/Scripts/BalloonInflator.cs
/// (ชื่อคลาส ชื่อไฟล์ และช่องทุกช่องเหมือนเดิม ค่าที่ผูกไว้ในฉากไม่หาย)
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

    [Tooltip("ค่าไกที่ถือว่า 'กดสุด' (ใช้เมื่อเปิด Release When Fully Squeezed)")]
    [SerializeField] float releaseThreshold = 0.98f;

    [Header("แก้บั๊กปั๊มไม่จำกัด")]
    [Tooltip("กดไกสุดแล้วปล่อยลูกโป่ง 1 ลูกต่อการกด 1 ครั้ง (ปิด = เป่าเล่นได้ไม่มีลูกหลุดเลย แนะนำ)")]
    [SerializeField] bool releaseWhenFullySqueezed = false;

    [Tooltip("ไกต้องคลายต่ำกว่าค่านี้ก่อน จึงนับการกดสุดครั้งใหม่ได้ (กันกดค้างแล้วปล่อยรัว)")]
    [SerializeField] float reArmThreshold = 0.3f;

    IXRSelectInteractable m_Interactable;
    XRInputButtonReader m_ActivateInput;
    Balloon m_BalloonInstance;
    bool m_IsHeld;

    // สถานะกันปล่อยลูกโป่งถี่เกิน: true = พร้อมนับการกดสุดครั้งใหม่
    bool m_ReleaseArmed = true;

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
        m_ReleaseArmed = true;

        // เก็บตัวอ่านค่าไกจากคอนโทรลเลอร์ของมือที่จับ (แบบเดียวกับ InteractionAnimator ใน XRI Examples)
        var controllerInteractor = args.interactorObject as XRBaseInputInteractor;
        m_ActivateInput = controllerInteractor != null ? controllerInteractor.activateInput : null;

        if (m_BalloonInstance == null)
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
        // กด = ขยายออกจากปลายท่อ / ปล่อยไก = หดกลับเล็กลง (เหมือนคลิปเพื่อน)
        m_BalloonInstance.transform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, trigger);

        // (ทางเลือก ปิดอยู่โดยปริยาย) กดไกสุด = ปล่อยลูกโป่ง 1 ลูก แล้วเตรียมลูกใหม่
        // มีกลอน reArm กันกดค้างไว้แล้วปั๊มรัว: ไกต้องคลายต่ำกว่า reArmThreshold
        // ก่อน จึงจะปล่อยลูกถัดไปได้ -> กด 1 ครั้ง ได้ลูก 1 ลูกเท่านั้น
        if (releaseWhenFullySqueezed && m_BalloonInstance != null)
        {
            if (trigger >= releaseThreshold && m_ReleaseArmed)
            {
                m_BalloonInstance.Detach();
                m_BalloonInstance = null;
                m_ReleaseArmed = false;
                SpawnBalloon();
            }
            else if (trigger <= reArmThreshold)
            {
                m_ReleaseArmed = true;
            }
        }
    }
}