namespace Icaro
{
    // Pose de um quadro do sprite da arma (WeaponSpritePainter): os quadros de parada, tiro e recarga são a mesma
    // arma desenhada com poses diferentes.
    public struct WeaponPose
    {
        // Graus a mais no ângulo da arma (positivo levanta o cano)
        public float Angle;

        // Deslocamento na tela (pixels do sprite; y para baixo)
        public float Dx;
        public float Dy;

        // Coice: a arma recua ao longo do cano (pixels) e o cano sobe um pouco
        public float Kick;

        // Clarão do cano: 0 nenhum, 1 grande, 2 pequeno
        public int Flash;

        // Cano alternativo (Liberdade: o de cima)
        public bool Alt;

        // Pente/tambor/caixa: 0 no lugar, 1 caído para fora
        public float MagDrop;

        // Sem pente (entre tirar o velho e pôr o novo)
        public bool MagGone;

        // Mão de apoio afastada para baixo (0 no lugar, 1 fora da tela)
        public float HandAway;

        // A mão de apoio segura o pente (trazendo o novo)
        public bool HandOnMag;

        // Revólver: tambor aberto (0 fechado, 1 aberto)
        public float Open;

        // Revólver: células gastas caindo (0 a 1); fora desse intervalo não aparecem
        public float Eject;

        // Revólver: células novas acendendo no tambor aberto (0 a 1)
        public float Charge;

        // Titan: aletas quentes (0 frias, 1 em brasa)
        public float Heat;
    }
}
