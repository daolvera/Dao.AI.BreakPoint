using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services.DTOs;

public interface IBaseDto<T> where T : BaseModel
{
    public T ToModel();
}
