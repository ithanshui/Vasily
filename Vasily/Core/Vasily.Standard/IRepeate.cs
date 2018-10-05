﻿namespace Vasily.Standard
{
    interface IRepeate
    {
        /// <summary>
        /// 根据model信息生成 SELECT COUNT(*) FROM [TableName] WHERE [Member1]=@Member1 AND [Member2]=@Member2 ....
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查重字符串结果</returns>
        string Repeate(MakerModel model);
    }
}