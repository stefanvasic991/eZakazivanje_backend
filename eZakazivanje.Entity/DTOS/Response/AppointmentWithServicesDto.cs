using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eZakazivanje.Entity.DbSet;

namespace eZakazivanje.Entity.DTOS.Response
{
    public class AppointmentWithServicesDto
    {
        public Appointment? Appointment { get; set; }
        public List<Guid>? ServiceIds { get; set; }
    }
}