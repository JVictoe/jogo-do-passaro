BIRD GROUND ANIMATIONS — v0.2.1 (TESTE)
============================================================
O QUE FOI CORRIGIDO
1. As pernas esquerda e direita agora recebem fases opostas e seu eixo de
   movimento e calculado com base na direcao real dos segmentos no mundo.
   Isso elimina a inversao dupla da antiga opcao "Espelhar perna direita".
2. As asas agora tem pose RECOLHIDA por meio de rotacao dos ossos
   wing_L_1_1 / wing_R_1_1 e articulacoes intermediarias.
3. Os pes recebem pequena elevacao alternada durante a passada.
4. O FBX original, clips de voo, Rigidbody e Animator Controller NAO
   sao modificados.

INSTALACAO
1. Faca backup/commit do projeto.
2. APAGUE o arquivo antigo Assets/Editor/BirdGroundAnimationGenerator.cs
   para nao ter duas classes com o mesmo nome.
3. Coloque BirdGroundAnimationGenerator_v02.cs em Assets/Editor/.
4. Com a Unity FORA do Play Mode, instancie uma COPIA LIMPA de Bird.fbx
   na cena. Evite usar o personagem Bird atual e nao deixe previews ativos.
5. Selecione a copia do modelo com Animator na Hierarchy.
6. Abra Tools > Bird Game > Ground Animation Generator v0.2.
7. Clique GERAR GroundIdle, Walk e Run (v0.2).
8. Os clips aparecem em Assets/_Game/Animations/BirdGround, com sufixo
   _v02 (e numeracao adicional se ja existirem).

TESTE
1. Em uma copia temporaria, associe um Animator Controller vazio
   com Bird_Walk_v02 como estado padrao e de Play.
2. Confira: (A) pernas alternam para frente e para tras; (B) asas ficam
   recolhidas; (C) pes se elevam alternadamente; (D) clip repete sem tranco.
3. Repita com Bird_Run_v02 e Bird_GroundIdle_v02.
4. Se a direcao da passada parecer invertida, use Inverter direcao da
   passada. Se o joelho dobrar ao contrario, use Inverter dobra do joelho.
5. Ajuste 'Fechar asas' (ex.: 55-90 graus), 'Dobrar ponta' (0-55 graus)
   se necessario. Regere e teste os NOVOS clips, sem alterar o rig original.
6. Se as asas ainda abrirem ou torcerem, envie screenshot lateral/frontal
   com nome da aba/clip: calcularemos a pose exata do rig.

CUIDADOS
- Os eixos sao estimados a partir do esqueleto e podem precisar de calibracao.
- O novo clip e uma primeira correção tecnica; nao e animacao refinada por
  animador e nao foi renderizado na Unity neste ambiente.
- Recomendamos importar Bird.fbx como Generic, e testar sem Root Motion.
- Nao conecte ainda ao BirdFlightAnimation, FlightLoop, Boost, Brake, Glide.
- Nao apague os clips v0.1 ate confirmar que nao estao referenciados.
