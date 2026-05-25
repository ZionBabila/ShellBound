using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private AudioSource source;

    //Sounds and Variables
    public AudioClip ouch;

    public AudioClip click;
    public AudioClip ghost;
    public AudioClip winLevel;
    public AudioClip loseLevel;
    public AudioClip hitFirst;
    public AudioClip hitSecond;
    public AudioClip hitLast;
    public AudioClip boo;
    public AudioClip bone1;
    public AudioClip bone2;
    public AudioClip bone3;
    public AudioClip bone4;


    public static bool OuchSound = false;
    public static bool hitFirstSound = false;
    public static bool hitSecondSound = false;
    public static bool hitLastSound = false;
    public static bool clickSound = false;
    public static bool ghostSound = false;
    public static bool winLevelSound = false;
    public static bool loseLevelSound = false;
    public static bool booSound = false;
    public static bool crackBones1 = false;
    public static bool crackBones2 = false;
    public static bool crackBones3 = false;
    public static bool crackBones4 = false;




    void Start()
    {
        source = GetComponent<AudioSource>();
        OuchSound = false;
        hitFirstSound = false;
        hitSecondSound = false;
        hitLastSound = false;
        clickSound = false;
        ghostSound = false;
        winLevelSound = false;
        loseLevelSound = false;
        booSound = false;
        crackBones1 = false;
        crackBones2 = false;
        crackBones3 = false;
        crackBones4 = false;

    }

    void Update()
    {
        Ouch();
        Click();
        Ghost();
        WinLevel();
        HitFirst();
        HitSecond();
        HitLast();
        LoseLevel();
        Boo();
        CrackB1();
        CrackB2();
        CrackB3();
        CrackB4();
    }

    private void CrackB1()
    {
        if (crackBones1 == true)
        {
            source.PlayOneShot(bone1);
            crackBones1 = false;
        }
    }
    private void CrackB2()
    {
        if (crackBones2 == true)
        {
            source.PlayOneShot(bone2);
            crackBones2 = false;
        }
    }
    private void CrackB3()
    {
        if (crackBones3 == true)
        {
            source.PlayOneShot(bone3);
            crackBones3 = false;
        }
    }
    private void CrackB4()
    {
        if (crackBones4 == true)
        {
            source.PlayOneShot(bone4);
            crackBones4 = false;
        }
    }
    private void Boo()
    {
        if (booSound == true)
        {
            source.PlayOneShot(boo);
            booSound = false;
        }
    }
    private void Ouch()
    {
        if (OuchSound == true)
        {
            source.PlayOneShot(ouch);
            OuchSound = false;
        }
    }
    private void Click()
    {
        if (clickSound == true)
        {
            source.PlayOneShot(click);
            clickSound = false;
        }
    }
    private void Ghost()
    {
        if (ghostSound == true)
        {
            source.PlayOneShot(ghost);
            ghostSound = false;
        }
    }
    private void WinLevel()
    {
        if (winLevelSound == true)
        {
            source.PlayOneShot(winLevel);
            winLevelSound = false;
        }
    }
    private void LoseLevel()
    {
        if (loseLevelSound == true)
        {
            source.PlayOneShot(loseLevel);
            loseLevelSound = false;
        }
    }
    private void HitFirst()
    {
        if (hitFirstSound == true)
        {
            source.PlayOneShot(hitFirst);
            hitFirstSound = false;
        }
    }
    private void HitSecond()
    {
        if (hitSecondSound == true)
        {
            source.PlayOneShot(hitSecond);
            hitSecondSound = false;
        }
    }
    private void HitLast()
    {
        if (hitLastSound == true)
        {
            source.PlayOneShot(hitLast);
            hitLastSound = false;
        }
    }
}
