using UnityEngine;

/// <summary>
/// BALLOON — ลูกโป่ง (อัปเดตจากตัวเดิมในโปรเจกต์ ยังใช้ร่วมกับ prefab เดิมได้ทันที)
/// สิ่งที่เพิ่มจากของเดิม:
/// - ปรับแรงลอยขึ้นได้ (floatUpForce)
/// - มีเสียง/เอฟเฟกต์ตอนแตก (Pop) แบบใส่หรือไม่ใส่ก็ได้
/// - ลูกโป่งที่ปล่อยลอยไปจะค่อยๆ หายเอง (autoDestroyAfter) กันฉากรก
///
/// หมายเหตุ: ใช้แทนไฟล์ Balloon.cs เดิมได้เลย (ชื่อคลาสและ Detach() เหมือนเดิม)
/// </summary>
public class Balloon : MonoBehaviour
{
    [Header("การลอย")]
    [Tooltip("แรงดันให้ลูกโป่งลอยขึ้น (ยิ่งมากยิ่งลอยเร็ว)")]
    [SerializeField] float floatUpForce = 1.5f;

    [Header("การแตก (ใส่หรือไม่ใส่ก็ได้)")]
    [Tooltip("เอฟเฟกต์ตอนแตก")]
    [SerializeField] ParticleSystem popEffect;

    [Tooltip("เสียงตอนแตก")]
    [SerializeField] AudioClip popSound;

    [Header("การทำลายตัวเอง")]
    [Tooltip("ลูกโป่งลอยไปกี่วินาทีแล้วหายเอง (0 = ไม่หาย)")]
    [SerializeField] float autoDestroyAfter = 12f;

    Rigidbody m_Rigidbody;

    void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Rigidbody.isKinematic = true;
        m_Rigidbody.useGravity = false;
    }

    /// <summary>
    /// ปล่อยลูกโป่งให้ลอยอิสระ (BalloonInflator เรียกเมื่อกดไกสุด หรือปล่อยเครื่องเป่า)
    /// </summary>
    public void Detach()
    {
        transform.SetParent(null);
        m_Rigidbody.isKinematic = false;
        m_Rigidbody.useGravity = false;

        // ส่งตัวขึ้นเบาๆ ก่อนแรงลอยจะทำงาน
        m_Rigidbody.linearVelocity = Vector3.up * 0.5f;

        var force = gameObject.AddComponent<ConstantForce>();
        force.force = Vector3.up * floatUpForce;

        if (autoDestroyAfter > 0f)
            Destroy(gameObject, autoDestroyAfter);
    }

    /// <summary>
    /// ให้ลูกโป่งแตก (เรียกใช้เองได้ เช่น ผูกกับ event ตอนโดนของแหลม)
    /// </summary>
    public void Pop()
    {
        if (popEffect != null)
        {
            var fx = Instantiate(popEffect, transform.position, Quaternion.identity);
            Destroy(fx.gameObject, 2f);
        }

        if (popSound != null)
            AudioSource.PlayClipAtPoint(popSound, transform.position);

        Destroy(gameObject);
    }
}
