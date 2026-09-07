using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ConnectUI : MonoBehaviour
{
    public Sprite lightOnSprite;
    public Sprite lightOffSprite;

    private Image[] lights;
    private int currentLightIndex = 1;

    void Start()
    {
        lights = new Image[3];
        lights[0] = transform.Find("1").GetComponent<Image>();
        lights[1] = transform.Find("2").GetComponent<Image>();
        lights[2] = transform.Find("3").GetComponent<Image>();

        lights[0].sprite = lightOnSprite;
        lights[1].sprite = lightOffSprite;
        lights[2].sprite = lightOffSprite;

        StartCoroutine(LightCycle());
    }

    IEnumerator LightCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].sprite = (i == currentLightIndex) ? lightOnSprite : lightOffSprite;
            }

            currentLightIndex = (currentLightIndex + 1) % 3;
        }
    }
}
