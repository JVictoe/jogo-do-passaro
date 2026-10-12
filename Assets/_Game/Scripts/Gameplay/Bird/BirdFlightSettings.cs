
using UnityEngine;
using UnityEngine.Serialization;

namespace Scripts.Gameplay.Bird
{
    [CreateAssetMenu(
        fileName = "BirdFlightSettings",
        menuName = "Bird/Flight Settings"
    )]

    public sealed class BirdFlightSettings : ScriptableObject
    {

        #region Speed

        [Header("Speed")]

        [Tooltip(
            "Velocidade minima de referencia para o sistema de voo. " +
            "O valor 0 permite que o passaro fique parado no ar (hover). " +
            "Tambem e utilizada para normalizar a velocidade em outros calculos."
        )]
        [SerializeField, Min(0f)]
        private float minSpeed = 0f;

        [Tooltip(
            "Velocidade normal desejada ao segurar W sem Shift. " +
            "Pode ser modificada temporariamente por subidas, " +
            "mergulhos e pelo impulso acumulado."
        )]
        [FormerlySerializedAs("startSpeed")]
        [SerializeField, Min(0f)]
        private float cruiseSpeed = 8f;

        [Tooltip(
            "Velocidade-base desejada ao segurar W + Shift. " +
            "Nao e um limite absoluto: o mergulho e o Dive Momentum " +
            "podem aumentar temporariamente a velocidade acima deste valor."
        )]
        [SerializeField, Min(0f)]
        private float maxSpeed = 18f;

        [Tooltip(
            "Taxa de aumento da velocidade comandada, em unidades/s². " +
            "Valores maiores fazem o passaro acelerar mais rapidamente. " +
            "Nao controla diretamente a forca aplicada ao Rigidbody."
        )]
        [SerializeField]
        private float acceleration = 7f;

        [Tooltip(
            "Taxa de reducao da velocidade comandada ao soltar W, " +
            "em unidades/s². Valores maiores fazem o passaro " +
            "desacelerar mais rapidamente ate o hover. " +
            "A frenagem com S utiliza Emergency Braking."
        )]
        [SerializeField]
        private float braking = 10f;

        #endregion


        #region Flight

        [Header("Flight")]

        [Tooltip(
            "Velocidade angular base com que a direcao de voo acompanha " +
            "a direcao da camera, em graus por segundo. " +
            "Valores maiores tornam as curvas mais rapidas. " +
            "Pode ser modificada por High Speed Turn Multiplier."
        )]
        [SerializeField]
        private float turnSpeed = 180f;

        [Tooltip(
            "Intensidade da correcao fisica que faz o Rigidbody " +
            "acompanhar a velocidade desejada. " +
            "Valores maiores reduzem a inercia percebida e deixam " +
            "o movimento mais responsivo. " +
            "Valores menores produzem uma resposta mais gradual."
        )]
        [SerializeField]
        private float velocityResponse = 6f;

        [Tooltip(
            "Limite da aceleracao fisica aplicada ao Rigidbody " +
            "para corrigir sua velocidade, em unidades/s². " +
            "Valores maiores permitem correcoes mais fortes, " +
            "principalmente nas mudancas bruscas de direcao. " +
            "Durante a frenagem ativa, esse limite recebe " +
            "o Brake Response Multiplier."
        )]
        [SerializeField]
        private float maxFlightAcceleration = 80f;

        #endregion


        #region Turn Agility

        [Header("Turn Agility")]

        [Tooltip(
            "Multiplicador da velocidade angular de giro em alta velocidade. " +
            "1.0 mantem a velocidade de giro base. " +
            "1.2 permite girar ate 20% mais rapidamente. " +
            "0.7 reduz a velocidade de giro em ate 30%. " +
            "O efeito e aplicado progressivamente conforme a velocidade."
        )]
        [SerializeField, Range(0.4f, 1.4f)]
        private float highSpeedTurnMultiplier = 1f;

        #endregion


        #region Visual Banking

        [Header("Visual Banking")]

        [Tooltip(
            "Angulo maximo de inclinacao lateral do passaro, em graus. " +
            "Limita a inclinacao produzida pelas curvas e pelas teclas A/D. " +
            "Afeta apenas o modelo visual, nao a rotacao do Rigidbody."
        )]
        [SerializeField]
        private float maxBankAngle = 40f;

        [Tooltip(
            "Intensidade da inclinacao lateral produzida pelas curvas. " +
            "Multiplica a velocidade angular de giro para calcular " +
            "o banking. Valores maiores fazem o passaro inclinar " +
            "mais durante as curvas."
        )]
        [SerializeField]
        private float turnBankFactor = 0.25f;

        [Tooltip(
            "Angulo adicional de inclinacao lateral ao pressionar A ou D. " +
            "Valores maiores tornam o movimento visual dessas teclas " +
            "mais pronunciado. Nao altera diretamente a direcao fisica do voo."
        )]
        [SerializeField]
        private float keyboardBankAngle = 12f;

        [Tooltip(
            "Intensidade da suavizacao da rotacao final do modelo visual. " +
            "Valores maiores fazem o BirdVisualPivot acompanhar " +
            "a orientacao desejada mais rapidamente. " +
            "Valores menores produzem transicoes mais suaves e lentas."
        )]
        [SerializeField]
        private float visualSmoothing = 10f;

        #endregion


        #region Banking Feel

        [Header("Banking Feel")]

        [Tooltip(
            "Tempo de suavizacao para entrar em uma inclinacao lateral, " +
            "em segundos. Valores menores fazem o passaro inclinar " +
            "mais rapidamente ao iniciar ou intensificar uma curva. " +
            "Utilizado pelo Mathf.SmoothDampAngle."
        )]
        [SerializeField, Min(0.01f)]
        private float bankEnterSmoothTime = 0.12f;

        [Tooltip(
            "Tempo de suavizacao para reduzir a inclinacao lateral, " +
            "em segundos. Valores maiores fazem o passaro retornar " +
            "mais lentamente a postura nivelada. " +
            "Valores menores tornam a recuperacao mais rapida."
        )]
        [SerializeField, Min(0.01f)]
        private float bankExitSmoothTime = 0.32f;

        [Tooltip(
            "Define quanto a velocidade influencia a intensidade " +
            "da inclinacao lateral nas curvas. " +
            "0 desativa essa variacao. " +
            "Valores maiores reduzem o banking em baixa velocidade " +
            "e o intensificam em alta velocidade."
        )]
        [SerializeField, Range(0f, 1f)]
        private float bankSpeedInfluence = 0.35f;

        #endregion


        #region Active Braking

        [Header("Active Braking")]

        [Tooltip(
            "Taxa de reducao da velocidade comandada ao pressionar S, " +
            "em unidades/s². Deve ser maior que Braking para oferecer " +
            "uma frenagem ativa mais rapida do que soltar W."
        )]
        [SerializeField, Min(0f)]
        private float emergencyBraking = 12f;

        [Tooltip(
            "Multiplicador da resposta fisica do Rigidbody durante " +
            "a frenagem com S. Aumenta Velocity Response e " +
            "Max Flight Acceleration enquanto o freio estiver ativo. " +
            "1.0 nao adiciona reforco fisico."
        )]
        [SerializeField, Min(1f)]
        private float brakeResponseMultiplier = 1.8f;

        [Tooltip(
            "Angulo de inclinacao do corpo para tras durante " +
            "a frenagem ativa, em graus. " +
            "Valores maiores fazem o passaro levantar mais o nariz, " +
            "simulando uma postura de frenagem aerodinamica. " +
            "Afeta apenas o modelo visual."
        )]
        [SerializeField, Range(0f, 50f)]
        private float brakePitchAngle = 30f;

        [Tooltip(
            "Rapidez com que o modelo assume a postura de frenagem " +
            "quando S e pressionado. " +
            "Valores maiores produzem uma inclinacao mais imediata. " +
            "Valores menores tornam a transicao mais gradual."
        )]
        [SerializeField, Min(0f)]
        private float brakeTiltEnterResponse = 14f;

        [Tooltip(
            "Rapidez com que o modelo retorna a postura normal " +
            "ao terminar a frenagem. " +
            "Valores maiores produzem uma recuperacao mais rapida. " +
            "Valores menores deixam o retorno mais suave e demorado."
        )]
        [SerializeField, Min(0f)]
        private float brakeTiltExitResponse = 8f;


        [Tooltip(
            "Velocidade fisica em que a animacao final " +
            "de frenagem comeca a ser reproduzida."
        )]
        [SerializeField, Min(0.1f)]
        private float brakeAnimationStartSpeed = 6f;

        [Tooltip(
            "Tempo usado para desacelerar suavemente " +
            "durante a animacao final de frenagem. " +
            "Deve corresponder a duracao do clip Brake."
        )]
        [SerializeField, Min(0.1f)]
        private float brakeFinishDuration = 1.8f;


        #endregion


        #region Vertical Flight Feel

        [Header("Vertical Flight Feel")]

        [Tooltip(
            "Intensidade da variacao de velocidade causada " +
            "pela inclinacao vertical da trajetoria. " +
            "Ao mergulhar, aumenta a velocidade-alvo. " +
            "Ao subir, reduz a velocidade-alvo. " +
            "0 desativa esse efeito. " +
            "Com valor 2, um mergulho vertical adiciona ate 2 " +
            "unidades/s e uma subida vertical retira ate 2."
        )]
        [SerializeField, Min(0f)]
        private float verticalSpeedInfluence = 2f;

        #endregion


        #region Dive Momentum

        [Header("Dive Momentum")]

        [Tooltip(
            "Ativa ou desativa a acumulacao de impulso durante " +
            "os mergulhos. Quando ativado, o passaro pode conservar " +
            "temporariamente parte da velocidade adicional ao nivelar " +
            "o voo ou iniciar uma subida. " +
            "Desative para comparar com o comportamento da v0.4."
        )]
        [SerializeField]
        private bool enableDiveMomentum = true;

        [Tooltip(
            "Quantidade maxima de velocidade adicional que pode " +
            "ser armazenada durante um mergulho, em unidades/s. " +
            "Por exemplo, 3 permite adicionar ate 3 unidades/s " +
            "a velocidade-alvo enquanto houver impulso armazenado. " +
            "Nao e um limite de velocidade total."
        )]
        [SerializeField, Min(0f)]
        private float maxDiveMomentum = 3f;

        [Tooltip(
            "Taxa maxima de acumulacao de impulso durante " +
            "mergulhos validos, em unidades/s². " +
            "O ganho depende da intensidade da descida. " +
            "Valores maiores permitem atingir Max Dive Momentum " +
            "mais rapidamente."
        )]
        [SerializeField, Min(0f)]
        private float diveMomentumGain = 1.5f;

        [Tooltip(
            "Taxa de perda do impulso armazenado quando o passaro " +
            "nao esta realizando um mergulho valido nem subindo " +
            "acentuadamente, em unidades/s². " +
            "Valores menores conservam o impulso por mais tempo " +
            "durante o voo nivelado."
        )]
        [SerializeField, Min(0f)]
        private float diveMomentumDecay = 0.8f;

        [Tooltip(
            "Taxa de perda do impulso armazenado durante a subida, " +
            "em unidades/s². " +
            "Valores maiores fazem a energia do mergulho acabar " +
            "mais rapidamente. Valores menores permitem aproveitar " +
            "o impulso por mais tempo na subida."
        )]
        [SerializeField, Min(0f)]
        private float climbMomentumDecay = 1.5f;

        #endregion



        public bool EnableDiveMomentum => enableDiveMomentum;
        public float MaxDiveMomentum => maxDiveMomentum;
        public float DiveMomentumGain => diveMomentumGain;
        public float DiveMomentumDecay => diveMomentumDecay;
        public float ClimbMomentumDecay => climbMomentumDecay;

        public float MinSpeed => minSpeed;
        public float CruiseSpeed => cruiseSpeed;
        public float MaxSpeed => maxSpeed;
        public float Acceleration => acceleration;
        public float Braking => braking;

        public float TurnSpeed => turnSpeed;
        public float VelocityResponse => velocityResponse;
        public float MaxFlightAcceleration => maxFlightAcceleration;

        public float MaxBankAngle => maxBankAngle;
        public float TurnBankFactor => turnBankFactor;
        public float KeyboardBankAngle => keyboardBankAngle;
        public float VisualSmoothing => visualSmoothing;

        public float BankEnterSmoothTime => bankEnterSmoothTime;
        public float BankExitSmoothTime => bankExitSmoothTime;
        public float BankSpeedInfluence => bankSpeedInfluence;
        public float HighSpeedTurnMultiplier => highSpeedTurnMultiplier;

        public float EmergencyBraking => emergencyBraking;

        public float BrakeResponseMultiplier => brakeResponseMultiplier;
        public float BrakePitchAngle => brakePitchAngle;
        public float BrakeTiltEnterResponse => brakeTiltEnterResponse;
        public float BrakeTiltExitResponse => brakeTiltExitResponse;

        public float VerticalSpeedInfluence => verticalSpeedInfluence;

        public float BrakeAnimationStartSpeed => brakeAnimationStartSpeed;

        public float BrakeFinishDuration => brakeFinishDuration;

        private void OnValidate()
        {
            minSpeed = Mathf.Max(0f, minSpeed);
            maxSpeed = Mathf.Max(minSpeed, maxSpeed);
            cruiseSpeed = Mathf.Clamp(cruiseSpeed, minSpeed, maxSpeed);

            acceleration = Mathf.Max(0f, acceleration);
            braking = Mathf.Max(0f, braking);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            velocityResponse = Mathf.Max(0.1f, velocityResponse);
            maxFlightAcceleration = Mathf.Max(0.1f, maxFlightAcceleration);

            emergencyBraking = Mathf.Max(braking, emergencyBraking);
            brakeResponseMultiplier = Mathf.Max(1f, brakeResponseMultiplier);
            brakeTiltEnterResponse = Mathf.Max(0f, brakeTiltEnterResponse);
            brakeTiltExitResponse = Mathf.Max(0f, brakeTiltExitResponse);

            maxBankAngle = Mathf.Clamp(maxBankAngle, 0f, 80f);
            turnBankFactor = Mathf.Max(0f, turnBankFactor);
            keyboardBankAngle = Mathf.Max(0f, keyboardBankAngle);
            visualSmoothing = Mathf.Max(0f, visualSmoothing);

            bankEnterSmoothTime = Mathf.Max(0.01f, bankEnterSmoothTime);
            bankExitSmoothTime = Mathf.Max(0.01f, bankExitSmoothTime);
            bankSpeedInfluence = Mathf.Clamp01(bankSpeedInfluence);

            verticalSpeedInfluence = Mathf.Max(0f, verticalSpeedInfluence);

            maxDiveMomentum = Mathf.Max(0f, maxDiveMomentum);
            diveMomentumGain = Mathf.Max(0f, diveMomentumGain);
            diveMomentumDecay = Mathf.Max(0f, diveMomentumDecay);
            climbMomentumDecay = Mathf.Max(0f, climbMomentumDecay);

            brakeAnimationStartSpeed = Mathf.Max(0.1f, brakeAnimationStartSpeed);

            brakeFinishDuration = Mathf.Max(0.1f, brakeFinishDuration);
        }
    }
}
