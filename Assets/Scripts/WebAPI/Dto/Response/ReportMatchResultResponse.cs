namespace LOP
{
    public class ReportMatchResultResponse : HttpResponse
    {
        public ConfirmedParticipantDto[] participants;
    }

    public class ConfirmedParticipantDto
    {
        public string userId;
        public int placement;
        public int mmrBefore;
        public int mmrAfter;
    }
}
