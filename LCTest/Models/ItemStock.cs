﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LCTest.Models
{
    public class ItemStock
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ClassId { get; set; }
        public int Stock { get; set; }
        public bool IsModify { get; set; }
    }
}