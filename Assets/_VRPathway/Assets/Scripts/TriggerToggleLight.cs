using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

/// <summary>
/// TRIGGER TOGGLE LIGHT — ปุ่มกดเปิด/ปิดไฟ (รุ่นแก้บั๊ก)
///
/// ทำไมรุ่นเก่ากดแล้วไฟไม่ติด:
/// รุ่นเก่าต้อง "จับปุ่มค้างไว้ + เบาไก" ถึงจะสลับไฟ แต่ถ้าเรากดแบบปุ่มจริง
/// (ชี้ที่ปุ่มแล้วกด 1 ที หรือแตะแล้วปล่อย) โค้ดเก่าจะไม่ทำงานเลย
/// อีกทั้งปุ่มถูกวางลอยกลางอากาศพร้อม Rigidbody ที่มี gravity
/// พอกด Play ปุ่มจึงร่วงลงไปกองกลางพื้น และไม่มีสี/เสียงบอกสถานะ
///
/// รุ่นใหม่ทำงานยังไง:
/// - "กด" ปุ่ม 1 ครั้ง (ชี้ + กดไก แบบรีโมท หรือเอาคอนโทรลเลอร์แตะใกล้ๆ แล้วกดไก)
///   = สลับไฟทันที ไม่ต้องจับค้าง
/// - ปุ่มเปลี่ยนสี: เขียว = ไฟเปิด, แดง = ไฟปิด (เห็นชัดทันทีแม้ห้องสว่าง)
/// - ปุ่มยุบลง-เด้งกลับตอนกด + เสียงคลิก
/// - หยุดปัญหาปุ่มหล่น/กลิ้ง: บังคับ Rigidbody เป็น Kinematic ให้อยู่กับที่
/// - ถ้าบน object ไม่มี Interactable เลย สคริปต์จะเพิ่ม XRSimpleInteractable ให้เอง
///   (แค่มี Collider + สคริปต์นี้ ก็กดได้แล้ว)
/// - ยังเก็บโหมดเดิม "จับค้าง + เบาไก" ไว้ได้ (โหมด TriggerWhileHeld)
///   เผื่ออยากทำเป็นรีโมทถือไว้ในมือ
///
/// วิธีติดตั้ง (ปุ่มกดบนพื้น):
/// 1. Copy ไฟล์นี้ไปทับไฟล์เดิมที่ Assets/_VRPathway/Assets/Scripts/TriggerToggleLight.cs
///    (ชื่อคลาส ชื่อไฟล์ และช่อง controlledLights / clickSound / clickVolume เหมือนเดิม
///     ค่าที่ผูกไว้ในฉากจึงไม่หาย ไม่ต้องลากใหม่)
/// 2. เลือก object "Light Switch" ในฉาก แล้วลบ XR Grab Interactable ออก
///    (คลิกขวาที่หัว component > Remove Component) — ไม่ลบก็ได้ แต่ปุ่มจะติดมือตอนกด
///    ถ้าอยากใส่แทน: Add Component > XR Simple Interactable
///    (หรือไม่ต้องใส่เลย เพราะสคริปต์ใส่ให้เองตอนกด Play)
/// 3. ลากปุ่มไปวางบนพื้นตรงตำแหน่งที่ต้องการ (ใกล้แผงควบคุม) — ตั้งแต่รุ่นนี้ปุ่มจะไม่ร่วงแล้ว
///    ปุ่มขนาด 0.2 วางบนพื้นให้ตั้ง y ประมาณ 0.1
/// 4. ถ้าอยากให้ไฟเปลี่ยนชัดขึ้น ลากไฟดวงอื่นใส่ช่อง Controlled Lights เพิ่มได้หลายดวง
/// 5. กด Play แล้วชี้ที่ปุ่มด้วยคอนโทรลเลอร์ กดไก 1 ที = ไฟสลับ
///    ปุ่มจะเป็นสีเขียว (เปิด) / แดง (ปิด) มีเสียงคลิก และยุบตัวทุกครั้ง
///
/// หมายเหตุ: ถ้าอยากให้เริ่มเกมแบบ "ไฟปิด" แล้วกดปุ่มเพื่อเปิด
/// ให้ไปที่ Light ใน Inspector แล้วไม่ติก Enabled ก่อนกด Play
/// ปุ่มจะเริ่มเป็นสีแดง และกดครั้งแรกไฟจะติดทันที
/// </summary>
public class TriggerToggleLight : MonoBehaviour
{
    public enum ToggleMode
    {
        // กดปุ่ม 1 ครั้ง = สลับไฟทันที (แนะนำ ใช้กับปุ่มพื้น/ปุ่มติดผนัง)
        PressToToggle = 0,
        // จับปุ่มค้างไว้ แล้วเบาไก 1 ครั้ง = สลับไฟ (โหมดรีโมทแบบเดิม)
        TriggerWhileHeld = 1,
        // ทำงานทั้งสองแบบ
        Both = 2
    }

    [Header("ไฟที่คุม")]
    [Tooltip("ไฟทั้งหมดที่ปุ่มนี้คุม (ลาก Light ใส่ได้หลายดวง ยิ่งหลายดวงยิ่งเห็นชัด)")]
    [SerializeField] Light[] controlledLights;

    [Tooltip("GameObject ที่เปิด/ปิดพร้อมไฟ เช่น โคมไฟหรือเอฟเฟกต์ (ไม่ใส่ก็ได้)")]
    [SerializeField] GameObject[] alsoToggleGameObjects;

    [Header("วิธีกด")]
    [Tooltip("Press = กด 1 ครั้งสลับทันที (แนะนำ) / Trigger = จับค้างแล้วเบาไก")]
    [SerializeField] ToggleMode toggleMode = ToggleMode.PressToToggle;

    [Tooltip("ค่าไกที่ถือว่า 'กด' (0-1) ใช้เฉพาะโหมด TriggerWhileHeld")]
    [SerializeField] float triggerThreshold = 0.6f;

    [Header("เสียง")]
    [Tooltip("เสียงคลิกตอนสลับ (ใส่หรือไม่ใส่ก็ได้)")]
    [SerializeField] AudioClip clickSound;

    [Tooltip("ความดังเสียงคลิก")]
    [SerializeField] float clickVolume = 0.8f;

    [Header("ฟีดแบ็กของปุ่ม (ให้เห็นชัดว่ากดสำเร็จ)")]
    [Tooltip("ปุ่มยุบลงตอนกด (เมตร)")]
    [SerializeField] float pressDepth = 0.05f;

    [Tooltip("เวลาแอนิเมชันกดยุบ-เด้งกลับ (วินาที)")]
    [SerializeField] float pressDuration = 0.2f;

    [Tooltip("สีปุ่มตอนไฟ 'เปิด'")]
    [SerializeField] Color colorWhenOn = new Color(0.2f, 1f, 0.35f);

    [Tooltip("สีปุ่มตอนไฟ 'ปิด'")]
    [SerializeField] Color colorWhenOff = new Color(1f, 0.25f, 0.2f);

    [Tooltip("Renderer ของตัวปุ่ม (ว่าง = หาเองอัตโนมัติ)")]
    [SerializeField] Renderer buttonRenderer;

    [Header("กันปุ่มหล่น/กลิ้ง")]
    [Tooltip("บังคับ Rigidbody เป็น Kinematic ให้ปุ่มอยู่กับที่ ไม่ร่วงไม่กลิ้งตอนกด Play")]
    [SerializeField] bool makeImmovable = true;

    // ชื่อ property สีของ shader — รองรับทั้ง URP (_BaseColor) และ Built-in (_Color)
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");

    IXRSelectInteractable m_Interactable;
    XRSimpleInteractable m_AutoSimpleInteractable;
    XRInputButtonReader m_ActivateInput;
    bool m_IsHeld;
    bool m_IsOn;
    bool m_TriggerWasDown;
    Vector3 m_InitialLocalPosition;
    MaterialPropertyBlock m_Block;
    Coroutine m_PressRoutine;

    void Awake()
    {
        m_InitialLocalPosition = transform.localPosition;

        m_Block = new MaterialPropertyBlock();
        if (buttonRenderer == null)
            buttonRenderer = GetComponentInChildren<Renderer>();

        // สถานะเริ่มต้น = ตามไฟดวงแรก
        if (controlledLights != null && controlledLights.Length > 0 && controlledLights[0] != null)
            m_IsOn = controlledLights[0].enabled;

        // แก้ปัญหาปุ่มหล่น/กลิ้ง: ปุ่มจะอยู่ตรงที่วางไว้ใน Editor เสมอ
        var rb = GetComponent<Rigidbody>();
        if (rb != null && makeImmovable)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        UpdateButtonColor();
    }

    void OnEnable()
    {
        // หา Interactable ที่ใช้งานได้บน object นี้ (XR Simple หรือ XR Grab ก็ได้)
        m_Interactable = FindUsableInteractable();

        // ถ้าไม่มี Interactable เลย (เช่น ลบ XR Grab Interactable ออกแล้ว)
        // ให้เพิ่ม XRSimpleInteractable ให้เอง — แค่มี Collider + สคริปต์นี้ก็กดได้
        if (m_Interactable == null)
        {
            m_AutoSimpleInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            m_Interactable = m_AutoSimpleInteractable;

            if (GetComponentInChildren<Collider>() == null)
                Debug.LogWarning($"[TriggerToggleLight] {name} ไม่มี Collider — ปุ่มจะกดไม่ได้ กรุณาเพิ่ม Box Collider", this);
        }

        m_Interactable.selectEntered.AddListener(OnPressed);
        m_Interactable.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        if (m_Interactable as Object != null)
        {
            m_Interactable.selectEntered.RemoveListener(OnPressed);
            m_Interactable.selectExited.RemoveListener(OnReleased);
        }
    }

    IXRSelectInteractable FindUsableInteractable()
    {
        // เอาเฉพาะ interactable ที่ยังเปิดใช้งานอยู่ (ถ้า disable ไว้ให้ข้าม)
        foreach (var candidate in GetComponents<IXRSelectInteractable>())
        {
            var behaviour = candidate as Behaviour;
            if (behaviour != null && behaviour.isActiveAndEnabled)
                return candidate;
        }
        return null;
    }

    // ---------- เหตุการณ์ตอนกดปุ่ม ----------

    void OnPressed(SelectEnterEventArgs args)
    {
        m_IsHeld = true;

        // จำ interactor ที่กด เพื่ออ่านค่าไกได้ (สำหรับโหมด TriggerWhileHeld)
        var controllerInteractor = args.interactorObject as XRBaseInputInteractor;
        m_ActivateInput = controllerInteractor != null ? controllerInteractor.activateInput : null;

        // แก้บั๊กรุ่นเก่า: ตอน "คว้า/กด" ปุ่มด้วยไก ค่าไกมักกดอยู่แล้ว
        // ต้องจำสถานะตั้งต้นไว้ ไม่งั้นโหมดรีโมทจะสลับไฟเองทันทีที่จับ
        m_TriggerWasDown = ReadTriggerValue() >= triggerThreshold;

        // โหมดกด: แตะ/ชี้ + กด 1 ครั้ง = สลับไฟทันที
        if (toggleMode == ToggleMode.PressToToggle || toggleMode == ToggleMode.Both)
        {
            Toggle();
        }
    }

    void OnReleased(SelectExitEventArgs args)
    {
        m_IsHeld = false;
        m_ActivateInput = null;
    }

    void Update()
    {
        // โหมดกดล้วนไม่ต้องอ่านไกทุกเฟรม
        if (toggleMode != ToggleMode.TriggerWhileHeld && toggleMode != ToggleMode.Both)
            return;

        if (!m_IsHeld || m_ActivateInput == null)
            return;

        bool triggerDown = ReadTriggerValue() >= triggerThreshold;

        // นับเฉพาะ "ขอบขาขึ้น" ของการเบาไก (กดค้างไม่นับซ้ำ)
        if (triggerDown && !m_TriggerWasDown)
            Toggle();

        m_TriggerWasDown = triggerDown;
    }

    float ReadTriggerValue()
    {
        if (m_ActivateInput == null)
            return 0f;

        m_ActivateInput.TryReadValue(out float value);
        return value;
    }

    // ---------- สลับไฟ ----------

    void Toggle()
    {
        m_IsOn = !m_IsOn;
        ApplyState();
    }

    void ApplyState()
    {
        if (controlledLights != null)
        {
            foreach (var light in controlledLights)
            {
                if (light != null)
                    light.enabled = m_IsOn;
            }
        }

        if (alsoToggleGameObjects != null)
        {
            foreach (var go in alsoToggleGameObjects)
            {
                if (go != null)
                    go.SetActive(m_IsOn);
            }
        }

        UpdateButtonColor();
        PlayPressAnimation();

        if (clickSound != null)
            AudioSource.PlayClipAtPoint(clickSound, transform.position, clickVolume);

        // ไว้ดูใน Console ตอน debug — เห็นชัดว่าปุ่มทำงานถึงไหน
        Debug.Log($"[TriggerToggleLight] {name} : ไฟ = {(m_IsOn ? "เปิด (ON)" : "ปิด (OFF)")}", this);
    }

    // ---------- ฟีดแบ็กของปุ่ม ----------

    void UpdateButtonColor()
    {
        if (buttonRenderer == null)
            return;

        Color c = m_IsOn ? colorWhenOn : colorWhenOff;
        buttonRenderer.GetPropertyBlock(m_Block);
        m_Block.SetColor(BaseColorID, c);   // shader ของ URP
        m_Block.SetColor(ColorID, c);       // shader ของ Built-in
        buttonRenderer.SetPropertyBlock(m_Block);
    }

    void PlayPressAnimation()
    {
        if (!isActiveAndEnabled)
            return;

        if (m_PressRoutine != null)
            StopCoroutine(m_PressRoutine);
        m_PressRoutine = StartCoroutine(PressDownAndReturn());
    }

    IEnumerator PressDownAndReturn()
    {
        Vector3 up = m_InitialLocalPosition;
        Vector3 down = m_InitialLocalPosition + Vector3.down * pressDepth;
        float half = Mathf.Max(pressDuration * 0.5f, 0.01f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / half;
            transform.localPosition = Vector3.Lerp(up, down, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / half;
            transform.localPosition = Vector3.Lerp(down, up, t);
            yield return null;
        }

        transform.localPosition = up;
        m_PressRoutine = null;
    }

    // ---------- เรียกใช้จากภายนอกได้ ----------

    [Tooltip("เรียกจากปุ่ม UI อื่นก็ได้")]
    public void ToggleFromUI()
    {
        Toggle();
    }

    public void SetOn(bool on)
    {
        m_IsOn = on;
        ApplyState();
    }

    public bool IsOn => m_IsOn;
}
