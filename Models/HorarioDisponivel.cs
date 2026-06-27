namespace MapaMensal.Models;

public class HorarioDisponivel
{
    public int Id { get; set; }
    public int DiaSemana { get; set; } // 0=Dom, 1=Seg, 2=Ter, 3=Qua, 4=Qui, 5=Sex, 6=Sáb
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFim { get; set; }
    public int DuracaoSlotMinutos { get; set; } = 60;
    public bool Ativo { get; set; } = true;
}
