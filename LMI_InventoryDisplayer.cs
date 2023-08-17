/*
@ Title : Ship Info Assistant
@ Auther : liuming7
@ Version : 1.3
@ Tutorial :
    1.将以下注释标注的几个参数修改好
    2.将代码粘贴进可编程模块的“编辑”里（在“参数”里输入参数，点击“运行”即可运行指定操作，详细请见下文注释）
    3.在LCD中的“自定义数据”中输入要显示的数据名（Ore = 矿石，Ingot = 矿锭，Component = 零件 ，Ammo = 弹药,Tool = 工具，AllItem = 显示全部，Capacity = 显示货箱已使用容量）
    4.若新增了方块，请点击“重置代码”，否则将无法使用新方块
@ Description :
    2020.10.10
    1.重构了代码，实现了高可读性和高扩展性，大幅降低了内存消耗，避免内存泄露
    2.增加了代码错误提示和自动重启机制，使用户界面更加友好
    3.实现了整机全扫，将装配机（@Assembler）和精炼厂（@Refinery）的第二货仓扫描
    4.增强了系统高可用性，当船只受损时在之前的版本会导致停机，现在会在下一个周期刷新后正常显示
    5.实现了双列显示，适配了lcd字体尺寸的差异
    2022.03.09
    1.支持了最新的mod内容，包括弹药、武器和其他
    2.实现了屏幕自适应大小
    3.新增了三列显示方法，更加节省屏幕空间
    4.重构了代码，增强可读性和可维护性
    0.待实现：其他船只状态监控（充电情况，机械结构完整性）
*/
private const bool scheduled = true;//是否使用自动模式(输入参数"update"立刻更新库存)
private const bool checkCargo = false;//是否只检查箱子(否则检查所有有库存的块）
private const bool checkSubgrid = true;//是否检查子网格(否则只检测本机)
private const int skip = 200;//在这里设置自动模式运行间隔。
private const bool isFormat = true;//是否开启预设样式
private const int infoLengthLimit2 = 120;
private const int infoLengthLimit3 = 80;

//private static string lastUpdateTime;
private static int time;
private static int disfunction;
private static int displayRowCount;
private static float totalVolume;
private static float currentVolume;
private static float totalCargoVolume;
private static float currentCargoVolume;

private static Exception localException;
private static RuntimeTracker runtimeTracker;

private static List<MyInventoryItem> items = null;
private static Dictionary<string, float> oreItems = null;
private static Dictionary<string, float> ingotItems = null;
private static Dictionary<string, float> componentItems = null;
private static Dictionary<string, float> ammoItems = null;
private static Dictionary<string, float> toolItems = null;
private static Dictionary<string, float> otherItems = null;
private static List<IMyTextPanel> panels = null;
private static List<IMyTerminalBlock> containers = null;
private static List<IMyShipController> shipControllers = null;
private static IMyInventory inventory = null;
IMyProductionBlock productor = null;

private static Dictionary<string, string> translate = new Dictionary<string, string>(){
    {"Ore", "矿 石"},
    {"Ammo", "弹 药"},
	{"Tool", "工 具"},
    {"Component", "零 件"},
    {"Ingot", "矿 锭"},
	{"Capacity", "占 用"},
	{"OtherItem", "其 他"},
    {"AllItem","全 部"}
};
private static Dictionary<string, string> translateOre = new Dictionary<string, string>(){
    {"MyObjectBuilder_Ore/Stone", "石头"},
    {"MyObjectBuilder_Ore/Iron", "铁矿"},
    {"MyObjectBuilder_Ore/Nickel", "镍矿"},
    {"MyObjectBuilder_Ore/Cobalt", "钴矿"},
    {"MyObjectBuilder_Ore/Magnesium", "镁矿"},
    {"MyObjectBuilder_Ore/Silicon", "硅矿"},
    {"MyObjectBuilder_Ore/Silver", "银矿"},
    {"MyObjectBuilder_Ore/Gold", "金矿"},
    {"MyObjectBuilder_Ore/Platinum", "铂金矿"},
    {"MyObjectBuilder_Ore/Uranium", "铀矿"},
    {"MyObjectBuilder_Ore/Ice", "冰"},
    {"MyObjectBuilder_Ore/Scrap", "废金属"},
	{"MyObjectBuilder_Ore/Organic", "有机物"}
};
private static Dictionary<string, string> translateComponent = new Dictionary<string, string>(){
    {"MyObjectBuilder_Component/Construction", "结构零件"},
    {"MyObjectBuilder_Component/MetalGrid", "金属网格"},
    {"MyObjectBuilder_Component/InteriorPlate", "内衬板"},
    {"MyObjectBuilder_Component/SteelPlate", "钢板"},
    {"MyObjectBuilder_Component/Girder", "梁"},
    {"MyObjectBuilder_Component/SmallTube", "小钢管"},
    {"MyObjectBuilder_Component/LargeTube", "大钢管"},
    {"MyObjectBuilder_Component/Motor", "马达"},
    {"MyObjectBuilder_Component/Display", "显示器"},
    {"MyObjectBuilder_Component/BulletproofGlass", "防弹玻璃"},
    {"MyObjectBuilder_Component/Computer", "计算机"},
    {"MyObjectBuilder_Component/Reactor", "反应堆零件"},
    {"MyObjectBuilder_Component/Thrust", "推进器零件"},
    {"MyObjectBuilder_Component/GravityGenerator", "重力发生器零件"},
    {"MyObjectBuilder_Component/Medical", "医疗零件"},
    {"MyObjectBuilder_Component/RadioCommunication", "无线电零件"},
    {"MyObjectBuilder_Component/Detector", "探测器零件"},
    {"MyObjectBuilder_Component/Explosives", "爆炸物"},
    {"MyObjectBuilder_Component/SolarCell", "太阳能电池板"},
    {"MyObjectBuilder_Component/PowerCell", "动力电池"},
    {"MyObjectBuilder_Component/Superconductor", "超导体"},
    {"MyObjectBuilder_Component/Canvas", "帆布"},
    {"MyObjectBuilder_Component/ZoneChip", "区域筹码"},
	{"MyObjectBuilder_Datapad/Datapad", "数据板"},
	{"MyObjectBuilder_Package/Package", "包"},
	{"MyObjectBuilder_ConsumableItem/CosmicCoffee", "宇宙咖啡"},
	{"MyObjectBuilder_ConsumableItem/ClangCola", "叮当可乐"},
	{"MyObjectBuilder_ConsumableItem/Medkit", "医疗箱"},
	{"MyObjectBuilder_ConsumableItem/Powerkit", "电力装置"},
	{"MyObjectBuilder_PhysicalObject/SpaceCredit", "太空货币"}
};
private static Dictionary<string, string> translateAmmo = new Dictionary<string, string>(){
    {"MyObjectBuilder_AmmoMagazine/NATO_5p56x45mm", "纳托弹弹夹5.56X45mm"},
    {"MyObjectBuilder_AmmoMagazine/NATO_25x184mm", "纳托弹弹箱25X184mm"},
    {"MyObjectBuilder_AmmoMagazine/Missile200mm", "导弹收纳箱200mm"},
	{"MyObjectBuilder_AmmoMagazine/UltimateAutomaticRifleGun_Mag_30rd", "步枪MR-30E弹夹"},
    {"MyObjectBuilder_AmmoMagazine/RapidFireAutomaticRifleGun_Mag_50rd", "步枪MR-50A弹夹"},
    {"MyObjectBuilder_AmmoMagazine/PreciseAutomaticRifleGun_Mag_5rd", "步枪MR-8P弹夹"},
	{"MyObjectBuilder_AmmoMagazine/AutomaticRifleGun_Mag_20rd", "步枪MR-20弹夹"},
    {"MyObjectBuilder_AmmoMagazine/ElitePistolMagazine", "手枪S-10E弹夹"},
    {"MyObjectBuilder_AmmoMagazine/SemiAutoPistolMagazine", "手枪S-10弹夹"},
	{"MyObjectBuilder_AmmoMagazine/FullAutoPistolMagazine", "手枪S-20A弹夹"},
    {"MyObjectBuilder_AmmoMagazine/MediumCalibreAmmo", "突击加农炮炮弹"},
    {"MyObjectBuilder_AmmoMagazine/LargeCalibreAmmo", "火炮炮弹"},
	{"MyObjectBuilder_AmmoMagazine/AutocannonClip", "机炮弹夹"},
    {"MyObjectBuilder_AmmoMagazine/SmallRailgunAmmo", "小型轨道炮穿甲弹"},
    {"MyObjectBuilder_AmmoMagazine/LargeRailgunAmmo", "大型轨道炮穿甲弹"}
};
private static Dictionary<string, string> translateTool = new Dictionary<string, string>(){
    {"MyObjectBuilder_PhysicalGunObject/AutomaticRifleItem", "步枪MR-20"},
    {"MyObjectBuilder_PhysicalGunObject/PreciseAutomaticRifleItem", "步枪MR-8P"},
	{"MyObjectBuilder_PhysicalGunObject/RapidFireAutomaticRifleItem", "步枪MR-50A"},
	{"MyObjectBuilder_PhysicalGunObject/UltimateAutomaticRifleItem", "步枪MR-30E"},
	{"MyObjectBuilder_PhysicalGunObject/AdvancedHandHeldLauncherItem", "火箭筒PRO-1"},
	{"MyObjectBuilder_PhysicalGunObject/BasicHandHeldLauncherItem", "火箭筒RO-1"},
	{"MyObjectBuilder_PhysicalGunObject/SemiAutoPistolItem", "手枪S-10"},
	{"MyObjectBuilder_PhysicalGunObject/FullAutoPistolItem", "手枪S-20A"},
	{"MyObjectBuilder_PhysicalGunObject/ElitePistolItem", "手枪S-10E"},
	{"MyObjectBuilder_PhysicalGunObject/WelderItem", "焊接器"},
	{"MyObjectBuilder_PhysicalGunObject/Welder2Item", "一级增强焊接器"},
	{"MyObjectBuilder_PhysicalGunObject/Welder3Item", "二级精通焊接器"},
	{"MyObjectBuilder_PhysicalGunObject/Welder4Item", "三级精英焊接器"},
	{"MyObjectBuilder_PhysicalGunObject/AngleGrinderItem", "切割机"},
	{"MyObjectBuilder_PhysicalGunObject/AngleGrinder2Item", "一级增强切割机"},
	{"MyObjectBuilder_PhysicalGunObject/AngleGrinder3Item", "二级精通切割机"},
	{"MyObjectBuilder_PhysicalGunObject/AngleGrinder4Item", "三级精英切割机"},
	{"MyObjectBuilder_PhysicalGunObject/HandDrillItem", "手电钻"},
	{"MyObjectBuilder_PhysicalGunObject/HandDrill2Item", "一级增强手电钻"},
	{"MyObjectBuilder_PhysicalGunObject/HandDrill3Item", "二级精通手电钻"},
	{"MyObjectBuilder_PhysicalGunObject/HandDrill4Item", "三级精英手电钻"},
	{"MyObjectBuilder_OxygenContainerObject/OxygenBottle", "氧气瓶"},
	{"MyObjectBuilder_GasContainerObject/HydrogenBottle", "氢气瓶"}
};
private static Dictionary<string, string> translateIngot = new Dictionary<string, string>(){
    {"MyObjectBuilder_Ingot/Stone", "沙石"},
    {"MyObjectBuilder_Ingot/Iron", "铁锭"},
    {"MyObjectBuilder_Ingot/Nickel", "镍锭"},
    {"MyObjectBuilder_Ingot/Cobalt", "钴锭"},
    {"MyObjectBuilder_Ingot/Magnesium", "镁粉"},
    {"MyObjectBuilder_Ingot/Silicon", "硅锭"},
    {"MyObjectBuilder_Ingot/Silver", "银锭"},
    {"MyObjectBuilder_Ingot/Gold", "金锭"},
    {"MyObjectBuilder_Ingot/Platinum", "铂金锭"},
    {"MyObjectBuilder_Ingot/Uranium", "铀棒"}
};
private static Dictionary<string, float> ingotOut = new Dictionary<string, float>(){
    {"MyObjectBuilder_Ore/Stone", 0.028f},
    {"MyObjectBuilder_Ore/Iron", 1.4f},
    {"MyObjectBuilder_Ore/Nickel", 0.8f},
    {"MyObjectBuilder_Ore/Cobalt", 0.6f},
    {"MyObjectBuilder_Ore/Magnesium", 0.014f},
    {"MyObjectBuilder_Ore/Silicon", 1.4f},
    {"MyObjectBuilder_Ore/Silver", 0.2f},
    {"MyObjectBuilder_Ore/Gold", 0.02f},
    {"MyObjectBuilder_Ore/Platinum", 0.01f},
    {"MyObjectBuilder_Ore/Uranium", 0.02f},
    {"MyObjectBuilder_Ore/Scrap", 1.6f}
};

/*
liuming7
constractor:get blocks ,init the timer,build data structures
*/
public Program(){
    panels = new List<IMyTextPanel>();
    containers = new List<IMyTerminalBlock>();
    items = new List<MyInventoryItem>(90);
    oreItems = new Dictionary<string, float>(16);
    ingotItems = new Dictionary<string, float>(12);
    componentItems = new Dictionary<string, float>(36);
    ammoItems = new Dictionary<string, float>(24);
	toolItems = new Dictionary<string, float>(24);
    otherItems = new Dictionary<string, float>();
    totalVolume = 0;
    currentVolume = 0;
    disfunction = 0;
    displayRowCount = 0;
    time = skip+1;
    runtimeTracker = new RuntimeTracker(this, skip, 0.005);


    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(containers, block => (checkSubgrid ? true : block.CubeGrid == Me.CubeGrid) && (block.IsFunctional) && (checkCargo ? block is IMyCargoContainer : block.HasInventory));
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(panels, block => (block.CubeGrid == Me.CubeGrid) && (block.IsFunctional) && block is IMyTextPanel);
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers, block => (block.CubeGrid == Me.CubeGrid) && (block.IsFunctional) && block is IMyShipController);
    if (scheduled) Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

/*
liuming7
convert numbers to strings
*/
private string num2String(float amount){
    String str = amount>999999 ? $"{(amount / 1000000).ToString("F1")}M" : (amount > 999 ? $"{(amount / 1000).ToString("F1")}K" : $"{amount}");
    return str;
}

/*
liuming7
make tab between the informations of ore and ingot
*/
private string flush(string fstr, int num = 12, float mul = 1){
    string str = fstr;
    for(float i = fstr.Length * mul; i < num; i++){
        str += " ";
    }
    return str;
}

/*
liuming7
adjust fonts' width
*/
private int countLength(string str){
    int count = 0;
    foreach(char c in str){
        if(c=='|'||c=='1'||c=='.'||c=='-'||c==' '){
            count += 2;
        }else if(c=='2'||c=='3'||c=='4'||c=='5'||c=='6'||c=='7'||c=='8'||c=='9'||c=='0'||c=='K'||c=='S'||c=='E'||c=='P'||c=='O'){
            count += 4;
        }else if(c=='M'||c=='m'){
			count += 6;
		}else{
            count += 8;
        }
    }
    return count;
}

private void distinguishItem(List<MyInventoryItem> items){
    foreach (var item in items){
        string itemName = item.Type.ToString();
        if(translateOre.ContainsKey(itemName)){
            addItem(oreItems, item, itemName);
        }else if(translateIngot.ContainsKey(itemName)){
            addItem(ingotItems,item,itemName);
        }else if(translateComponent.ContainsKey(itemName)){
            addItem(componentItems,item,itemName);
        }else if(translateAmmo.ContainsKey(itemName)){
            addItem(ammoItems,item,itemName);
        }else if(translateTool.ContainsKey(itemName)){
            addItem(toolItems,item,itemName);
        }else{
            addItem(otherItems,item,itemName);
        }
    }
}

private void addItem(Dictionary<string, float> generalItems, MyInventoryItem item, string itemName){
    if (generalItems.ContainsKey(itemName)){
        generalItems[itemName] += item.Amount.ToIntSafe();
    }
    else generalItems.Add(itemName, item.Amount.ToIntSafe());
        
}

/*
liuming7
get each item and its volume from every container's inventory
*/
private void getItem(){
    oreItems.Clear();
    ingotItems.Clear();
    componentItems.Clear();
    ammoItems.Clear();
	toolItems.Clear();
	otherItems.Clear();
    totalVolume = 0;
    currentVolume = 0;
    totalCargoVolume = 0;
    currentCargoVolume = 0;
    foreach (var container in containers){
        if(!container.IsFunctional){
            continue;
        }
        items.Clear();
        inventory = container.GetInventory();
        inventory.GetItems(items);
		
		totalVolume += inventory.MaxVolume.ToIntSafe();
		currentVolume += inventory.CurrentVolume.ToIntSafe();
		
        if(container is IMyCargoContainer){
            totalCargoVolume += inventory.MaxVolume.ToIntSafe();
            currentCargoVolume += inventory.CurrentVolume.ToIntSafe();
        }

        distinguishItem(items);
        items.Clear();
        if(container is IMyProductionBlock){
            productor = (IMyProductionBlock)container;
            inventory = productor.OutputInventory;
            inventory.GetItems(items);

            if(productor is IMyAssembler){
                distinguishItem(items);
            }else if(productor is IMyRefinery){
                distinguishItem(items);
            }
        }
    }
}

/*
liuming7
display format
*/
private void display(int code){
    /*
    100:异常
    200:正常
    */
    getItem();
    foreach (var panel in panels){
        if(panel.CustomData == "" || panel.CustomData == null || !translate.ContainsKey(panel.CustomData)){
            continue;
        }
        string title = translate.ContainsKey(panel.CustomData) ? translate[panel.CustomData] : "通 用";
        panel.ContentType = ContentType.TEXT_AND_IMAGE;
        if(isFormat){
            panel.FontSize = 0.7f;
            panel.BackgroundColor = new Color(){R = 0,G = 25,B = 40,A = 0};
            panel.AddImageToSelection("LCD_Economy_Clear",true);
        }
        panel.WriteText($"[=========  LMI  ======{title}======  LMI  =========]\n");
        if(code==100){
            displayErrorMassage(panel);
        }else if(code==200){
            displayDispatcher(panel);
        }
        panel.WriteText($"[=========  LMI  ================  LMI  =========]\n\n", true);
        panel.WriteText($"*求救援？请呼叫LM救援*\n", true);
        panel.WriteText($"*收废品？请认准LM物能*\n", true);
        panel.WriteText($"*做美化？请认准LM文化*\n", true);
        panel.WriteText($"LM工业集团，裹挟你的生活\n", true);
        panel.WriteText($"|物能|救援|文化|金融|安防|\n", true);
        panel.WriteText($"©LMI 2014-2022\n", true);
    }
}

/*
liuming7
dispatcher
*/
private void displayDispatcher(IMyTextPanel panel){
    if(panel.CustomData == "Ore"){
        displayInventoryOre(panel);
    }else if(panel.CustomData == "Ingot"){
        displayInventoryIngot(panel);
    }else if(panel.CustomData == "Component"){
        displayInventoryComponent(panel);
    }else if(panel.CustomData == "Ammo"){
        displayInventoryAmmo(panel);
    }else if(panel.CustomData == "Tool"){
        displayInventoryTool(panel);
    }else if(panel.CustomData == "AllItem"){
        displayInventoryAll(panel);
    }else if(panel.CustomData == "Capacity"){
        displayInventoryCapacity(panel);
    }else if(panel.CustomData == "OtherItem"){
        displayInventoryOther(panel);
    }else{
        return;
    }
}

private void displaySingleCol(Dictionary<string, float> generalItems, Dictionary<string, string> translateGeneral, IMyTextPanel panel){
    foreach(var kvp in generalItems){
        panel.WriteText($"|{translateGeneral[kvp.Key]} : {num2String(kvp.Value)}\n", true);
        displayRowCount++;
    }
}

private void displayDoubleCol(Dictionary<string, float> generalItems, Dictionary<string, string> translateGeneral, IMyTextPanel panel){
    bool doubleCol = true;
    string temp = "";
    foreach(var kvp in generalItems){
        if(doubleCol){//是否首列
            temp = $"|{translateGeneral[kvp.Key]} : {num2String(kvp.Value)}";
            for(int i=countLength(temp);i<infoLengthLimit2;i+=2){
                temp += " ";
            }
            doubleCol = false;
        }else{
            panel.WriteText($"{temp}|{translateGeneral[kvp.Key]} : {num2String(kvp.Value)}\n", true);
            doubleCol = true;
			displayRowCount++;
        }
    }
    if(!doubleCol){
        panel.WriteText($"{temp}\n", true);
        doubleCol = true;
		displayRowCount++;
    }
}

private void displayTripleCol(Dictionary<string, float> generalItems, Dictionary<string, string> translateGeneral, IMyTextPanel panel){
	int tripleCol = 0;
    string temp = "";
    foreach(var kvp in generalItems){
        if(tripleCol!=2){//是否首列
            temp += $"|{translateGeneral[kvp.Key]} : {num2String(kvp.Value)}";
            for(int i=countLength(temp);i<(infoLengthLimit3+tripleCol*infoLengthLimit3);i+=2){
                temp += " ";
            }
            tripleCol++;
        }else{
            panel.WriteText($"{temp}|{translateGeneral[kvp.Key]} : {num2String(kvp.Value)}\n", true);
            tripleCol = 0;
			temp = "";
			displayRowCount++;
        }
    }
    if(tripleCol!=0){
        panel.WriteText($"{temp}\n", true);
        tripleCol = 0;
		temp = "";
		displayRowCount++;
    }
}

/*
liuming7
display ore
*/
private void displayInventoryOre(IMyTextPanel panel){
    foreach(var kvp in oreItems){
        panel.WriteText($"|{translateOre[kvp.Key]} : {flush(num2String(kvp.Value),12,1.6f)}", true);
        string assumption = "\n";
        if(ingotOut.ContainsKey(kvp.Key))
            assumption =  $"+预计{num2String(ingotOut[kvp.Key] * kvp.Value)}锭\n";
        panel.WriteText(assumption, true);
    }
}

/*
liuming7
display ingot
*/
private void displayInventoryIngot(IMyTextPanel panel){
    displaySingleCol(ingotItems,translateIngot,panel);
    displayRowCount = 0;
}

/*
liuming7
display component
*/
private void displayInventoryComponent(IMyTextPanel panel){
    bool isDoubleRow = componentItems.Count>14;
    if(isDoubleRow){
        displayDoubleCol(componentItems,translateComponent,panel);
    }else{
        displaySingleCol(componentItems,translateComponent,panel);
    }
    displayRowCount = 0;
}

/*
liuming7
display ammo
*/
private void displayInventoryAmmo(IMyTextPanel panel){
    displaySingleCol(ammoItems,translateAmmo,panel);
    displayRowCount = 0;
}

/*
liuming7
display tool
*/
private void displayInventoryTool(IMyTextPanel panel){
    displaySingleCol(toolItems,translateTool,panel);
    displayRowCount = 0;
}

/*
liuming7
display all
*/
private void displayInventoryAll(IMyTextPanel panel){
    displayTripleCol(oreItems,translateOre,panel);
    panel.WriteText($"|--------------------------------------------------------------------------------\n", true);
    displayTripleCol(ingotItems,translateIngot,panel);
    panel.WriteText($"|--------------------------------------------------------------------------------\n", true);
    displayTripleCol(componentItems,translateComponent,panel);
    panel.WriteText($"|--------------------------------------------------------------------------------\n", true);
    displayDoubleCol(ammoItems,translateAmmo,panel);
    panel.WriteText($"|--------------------------------------------------------------------------------\n", true);
    displayDoubleCol(toolItems,translateTool,panel);
    panel.WriteText($"|--------------------------------------------------------------------------------\n", true);
    displayInventoryCapacity(panel);
	
	displayRowCount += 9;
	if(displayRowCount>25){
		panel.FontSize = (25*0.7f)/displayRowCount;
	}
    displayRowCount = 0;
}

/*
liuming7
display capacity
*/
private void displayInventoryCapacity(IMyTextPanel panel){
    if(totalVolume==0){
        panel.WriteText($"总库存已占用: -% 物流系统不存在\n",true);
    }else{
        panel.WriteText($"总库存已占用: {(currentVolume / totalVolume * 100).ToString("F2")}%\n",true);
    }
    if(totalCargoVolume==0){
        panel.WriteText($"货箱库存已占用: -% 货箱不存在\n",true);
    }else{
        panel.WriteText($"货箱库存已占用: {(currentCargoVolume / totalCargoVolume * 100).ToString("F2")}%\n",true);
    }
}

/*
liuming7
display other
*/
private void displayInventoryOther(IMyTextPanel panel){
    foreach(var kvp in otherItems){
        panel.WriteText($"|{kvp.Key} : {num2String(kvp.Value)}\n", true);
    }
    
}

/*
liuming7
display error
*/
private void displayErrorMassage(IMyTextPanel panel){
    panel.WriteText($"计算机异常停机，", true);
    if(disfunction == 4){
        panel.WriteText($"自动重启失败，请手动重启\n* 可编程模块已关闭\n", true);
    }else{
        panel.WriteText($"正在自动重启，重启次数：{disfunction+1}/4\n", true);
    }
    panel.WriteText($"{localException.StackTrace}\n", true);
}

/*
liuming7
@deprecated
*/
public void Save(){
    //lastUpdateTime = DateTime.Now.ToString();
}

/*
liuming7
main function
*/
public void Main(string argument, UpdateType updateSource){
    try{
        runtimeTracker.addRuntime();
        runtimeTracker.addInstructions();
        if (scheduled){
            if (time > skip){
                display(200);
                time = 0;
            }
            Echo($"{this.Runtime.LastRunTimeMs}\n{(int)(skip - time)}s\n{runtimeTracker.write()}");
            time++;
        }
        if (argument == "update"){
            display(200);
            time = 0;
        } 
        disfunction = 0;
        //Echo(DateTime.Now.ToString());
        //Echo((Runtime.LastRunTimeMs * 1000).ToString("F0"));
    }
    catch(Exception ex){
        localException = ex;
        Echo("运行错误" );
        display(100);
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(containers, block => (checkSubgrid ? true : block.CubeGrid == Me.CubeGrid) && (checkCargo ? block is IMyCargoContainer : block.HasInventory) && (block.IsFunctional));
        GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(panels, block => (block.CubeGrid == Me.CubeGrid) && (block.IsFunctional));
        if(disfunction == 4){
            disfunction = 0;
            Me.Enabled = false;
        }
        disfunction++;
    }
}

public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime {get; private set;}
    public double MaxInstructions {get; private set;}
    public double AverageRuntime {get; private set;}
    public double AverageInstructions {get; private set;}
    
    private readonly Queue<double> _runtimes;
    private readonly Queue<double> _instructions;
    private readonly StringBuilder _sb = new StringBuilder();
    private readonly int _instructionLimit;
    private readonly Program _program;

    public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.01)
    {
        _runtimes = new Queue<double>(Capacity);
        _instructions = new Queue<double>(Capacity);
        _program = program;
        Capacity = capacity;
        Sensitivity = sensitivity;
        _instructionLimit = _program.Runtime.MaxInstructionCount;
    }

    public void addRuntime()
    {
        double runtime = _program.Runtime.LastRunTimeMs;
        AverageRuntime = Sensitivity * (runtime - AverageRuntime) + AverageRuntime;
        
        _runtimes.Enqueue(runtime);
        if (_runtimes.Count == Capacity)
        {
            _runtimes.Dequeue();
        }
        MaxRuntime = _runtimes.Max();
    }

    public void addInstructions()
    {
        double instructions = _program.Runtime.CurrentInstructionCount;
        AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions;
        
        _instructions.Enqueue(instructions);
        if (_instructions.Count == Capacity)
        {
            _instructions.Dequeue();
        }
        MaxInstructions = _instructions.Max();
    }

    public string write()
    {
        _sb.Clear();
        _sb.AppendLine("\n_____________________________\nGeneral Runtime Info\n");
        _sb.AppendLine($"Avg instructions: {AverageInstructions:n2}");
        _sb.AppendLine($"Max instructions: {MaxInstructions:n0}");
        _sb.AppendLine($"Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
        _sb.AppendLine($"Avg runtime: {AverageRuntime:n4} ms");
        _sb.AppendLine($"Max runtime: {MaxRuntime:n4} ms");
        return _sb.ToString();
    }
}

class Compass
{
    public bool InGravity = false;
    public double Bearing = 0;

    Vector3D _absNorthVec;
    Color _backgroundColor;
    Color _tickColor;
    Color _textColor;
    Color _pipColor;
    Color _textBoxColor;
    bool _drawBearing;

    readonly Program _program;
    readonly Vector2 PIP_SIZE = new Vector2(25f, 25f);
    readonly Vector2 TEXT_BOX_SIZE = new Vector2(FONT_SIZE * BASE_TEXT_HEIGHT_PX * 2.5f, FONT_SIZE * BASE_TEXT_HEIGHT_PX + 4f);
    readonly Vector2 TEXT_BOX_HORIZ_SPACING = new Vector2(FONT_SIZE * BASE_TEXT_HEIGHT_PX * 0.6f, 0);

    const double RAD_TO_DEG = 180.0 / Math.PI;
    const double FOV = 130;
    const double HALF_FOV = FOV * 0.5;
    const int MAJOR_TICK_INTERVAL = 45;
    const int MINOR_TICKS = 3;
    const int MINOR_TICK_INTERVAL = (int)(MAJOR_TICK_INTERVAL / MINOR_TICKS);

    const float FONT_SIZE = 1.8f;
    const float MAJOR_TICK_HEIGHT = 50f;
    const float MINOR_TICK_HEIGHT = MAJOR_TICK_HEIGHT / 2f;
    const float TICK_WIDTH = 6f;
    const float BASE_TEXT_HEIGHT_PX = 28.8f;
    const string FONT = "White";

    readonly Dictionary<int, string> _cardinalDirectionDict = new Dictionary<int, string>()
    {
        { 0,   "N"},
        { 45,  "NE" },
        { 90,  "E" },
        { 135, "SE" },
        { 180, "S" },
        { 225, "SW" },
        { 270, "W" },
        { 315, "NW" },
        { 360, "N" },
    };

    public Compass(Program program, ref CompassConfig compassConfig)
    {
        _program = program;
        // Easy access to Echo for debugging
        UpdateConfigValues(ref compassConfig);
    }

    public void UpdateConfigValues(ref CompassConfig compassConfig)
    {
        _drawBearing = compassConfig.DrawBearing;
        _absNorthVec = compassConfig.AbsNorthVec;
        _backgroundColor = compassConfig.BackgroundColor;
        _tickColor = compassConfig.LineColor;
        _textColor = compassConfig.TextColor;
        _pipColor = compassConfig.PipColor;
        _textBoxColor = compassConfig.TextBoxColor;
    }

    public void CalculateParameters(ref Vector3D forward, ref Vector3D gravity)
    {
        //check if grav vector exists 
        if (Vector3D.IsZero(gravity))
        {
            InGravity = false;
            return;
        }
        InGravity = true;

        //get east vector 
        Vector3D relativeEastVec = gravity.Cross(_absNorthVec);

        //get relative north vector 
        Vector3D relativeNorthVec;
        Vector3D.Cross(ref relativeEastVec, ref gravity, out relativeNorthVec);

        //project forward vector onto a plane comprised of the north and east vectors 
        Vector3D forwardProjNorthVec;
        VectorMathRef.Projection(ref forward, ref relativeNorthVec, out forwardProjNorthVec);
        Vector3D forwardProjEastVec;
        VectorMathRef.Projection(ref forward, ref relativeEastVec, out forwardProjEastVec);
        Vector3D forwardProjPlaneVec = forwardProjEastVec + forwardProjNorthVec;

        //find angle from abs north to projected forward vector measured clockwise 
        Bearing = VectorMathRef.AngleBetween(ref forwardProjPlaneVec, ref relativeNorthVec) * RAD_TO_DEG;

        //check direction of angle 
        if (Vector3D.Dot(forward, relativeEastVec) < 0)
        {
            Bearing = 360 - Bearing; //because of how the angle is measured 
        }

        if (Bearing >= 359.5)
            Bearing = 0;
    }

    public void DrawScreen(IMyTextSurface surf, bool refreshSpriteCache, bool drawRadialCompass)
    {
        surf.ContentType = ContentType.SCRIPT;
        surf.Script = "";
        surf.ScriptBackgroundColor = _backgroundColor;

        Vector2 textureSize = surf.TextureSize;
        Vector2 screenCenter = textureSize * 0.5f;
        Vector2 viewportSize = surf.SurfaceSize;
        Vector2 scaleVec = viewportSize / 512f;
        float compassHeight = FONT_SIZE * BASE_TEXT_HEIGHT_PX + MAJOR_TICK_HEIGHT + PIP_SIZE.Y + 4f;
        float referenceHeight = drawRadialCompass ? 512f : compassHeight;
        float scale = Math.Min(1, viewportSize.Y / referenceHeight);
        scale = Math.Min(scale, viewportSize.X / 512f);
        //_program._detailedInfo.Append($"{scale}: {viewportSize}\n");

        using (var frame = surf.DrawFrame())
        {
            if (refreshSpriteCache)
            {
                frame.Add(new MySprite());
            }

            if (drawRadialCompass)
                DrawRadialCompass(frame, ref screenCenter, ref viewportSize, scale);
            else
                DrawHorizontalCompass(frame, ref screenCenter, ref viewportSize, scale);
        }
    }

    void DrawHorizontalCompass(MySpriteDrawFrame frame, ref Vector2 screenCenter, ref Vector2 viewport, float scale)
    {
        double pxPerDeg = viewport.X / FOV; // NOTE: Not affected by scale because I want to fill the entire width

        double lowerAngle = Bearing - HALF_FOV;
        int lowerAngleMinor = (int)(lowerAngle - (lowerAngle % MINOR_TICK_INTERVAL)); // Round up to the nearest minor tick

        double upperAngle = Bearing + HALF_FOV;
        int upperAngleMinor = (int)(upperAngle - (upperAngle % MINOR_TICK_INTERVAL)); // Round down to the nearest minor tick

        int numMinorTicks = (upperAngleMinor - lowerAngleMinor) / MINOR_TICK_INTERVAL;

        Vector2 offsetCenterPos = screenCenter + new Vector2(0, 8f * scale);
        Vector2 majorTickSize = new Vector2(TICK_WIDTH, scale * MAJOR_TICK_HEIGHT);
        Vector2 minorTickSize = new Vector2(TICK_WIDTH, scale * MINOR_TICK_HEIGHT);
        Vector2 minorTickPosOffset = new Vector2(0, scale * (MAJOR_TICK_HEIGHT - MINOR_TICK_HEIGHT) * 0.5f);
        Vector2 majorTextPosOffset = new Vector2(0, -scale * (4f + FONT_SIZE * BASE_TEXT_HEIGHT_PX + 0.5f * MAJOR_TICK_HEIGHT));
        Vector2 pipPosOffset = new Vector2(0, scale * 0.5f * (PIP_SIZE.Y + MAJOR_TICK_HEIGHT));
        Vector2 textBoxPosOffset = majorTextPosOffset + new Vector2(0, 0.5f * scale * FONT_SIZE * BASE_TEXT_HEIGHT_PX);
        Vector2 textBoxTextPos = offsetCenterPos + majorTextPosOffset;

        // Create pip
        MySprite pipSprite = MySprite.CreateSprite("Triangle", offsetCenterPos + pipPosOffset, scale * PIP_SIZE);
        pipSprite.Color = _pipColor;
        frame.Add(pipSprite);

        // Create tick sprite to reuse
        MySprite tickSprite = MySprite.CreateSprite("SquareSimple", offsetCenterPos, Vector2.Zero);
        tickSprite.Color = _tickColor;

        // Draw tick marks and labels
        int angle = lowerAngleMinor;
        for (int i = 0; i <= numMinorTicks; ++i)
        {
            double diff = angle - lowerAngle;

            tickSprite.Position = new Vector2((float)(diff * pxPerDeg), offsetCenterPos.Y);
            if (angle % MAJOR_TICK_INTERVAL == 0)
            { // Draw major tick
                tickSprite.Size = majorTickSize;
                int bearingAngle = GetBearingAngle(angle);
                string label = "";
                _cardinalDirectionDict.TryGetValue(bearingAngle, out label);
                MySprite text = MySprite.CreateText(label, FONT, _textColor, scale * FONT_SIZE);
                text.Position = tickSprite.Position + majorTextPosOffset;
                frame.Add(text);
            }
            else
            { // Draw minor tick
                tickSprite.Size = minorTickSize;
                tickSprite.Position += minorTickPosOffset;
            }
            frame.Add(tickSprite);
            angle += MINOR_TICK_INTERVAL;
        }

        if (_drawBearing)
        {
            // Draw angle text box
            Vector2 textBoxSize = scale * TEXT_BOX_SIZE;
            Vector2 textHorizOffset = TEXT_BOX_HORIZ_SPACING * scale;
            Vector2 textBoxCenter = offsetCenterPos + textBoxPosOffset;
            // background
            MySprite textBox = MySprite.CreateSprite("SquareSimple", textBoxCenter, textBoxSize);
            textBox.Color = _backgroundColor;
            frame.Add(textBox);
            // box
            textBox.Data = "AH_TextBox";
            textBox.Color = _textBoxColor;
            frame.Add(textBox);

            // Write digits
            string bearingStr = $"{Bearing:000}";
            // hundreds
            MySprite digit = MySprite.CreateText(bearingStr.Substring(0, 1), FONT, _textColor, FONT_SIZE * scale);
            digit.Position = textBoxTextPos - textHorizOffset;
            frame.Add(digit);
            // tens
            digit.Data = bearingStr.Substring(1, 1);
            digit.Position = textBoxTextPos;
            frame.Add(digit);
            // ones
            digit.Data = bearingStr.Substring(2, 1);
            digit.Position = textBoxTextPos + textHorizOffset;
            frame.Add(digit);
        }
    }

    const float RADIAL_COMPASS_LABEL_RADIUS = 150f;
    readonly Vector2 RADIAL_COMPASS_SIZE = new Vector2(500f, 500f);
    readonly Vector2 RADIAL_COMPASS_MAJOR_CLIP_SIZE = new Vector2(400f, 400f);
    readonly Vector2 RADIAL_COMPASS_MINOR_CLIP_SIZE = new Vector2(450f, 450f);
    readonly Vector2 RADIAL_COMPASS_LINE_SIZE = new Vector2(6f, 500f);
    readonly Vector2 RADIAL_COMPASS_PIP_LOCATION = new Vector2(0f, -190);
    void DrawRadialCompass(MySpriteDrawFrame frame, ref Vector2 screenCenter, ref Vector2 viewport, float scale)
    {
        double angleOffset = -Bearing;

        MySprite line = MySprite.CreateSprite("SquareSimple", screenCenter, scale * RADIAL_COMPASS_LINE_SIZE);
        line.Color = _tickColor;
        // Draw minor ticks
        for (int angle = 0; angle < 180;  angle += MINOR_TICK_INTERVAL)
        {
            if (angle % MAJOR_TICK_INTERVAL == 0)
            {
                continue;
            }
            float rotation = (float)MathHelper.ToRadians(angle + angleOffset);
            line.RotationOrScale = rotation;
            frame.Add(line);
        }

        // Clip center of minor ticks
        MySprite circleClip = MySprite.CreateSprite("Circle", screenCenter, scale * RADIAL_COMPASS_MINOR_CLIP_SIZE);
        circleClip.Color = _backgroundColor;
        frame.Add(circleClip);

        float scaledFontSize = FONT_SIZE * scale;
        Vector2 fontVertOffset = new Vector2(0f, -scaledFontSize * BASE_TEXT_HEIGHT_PX * 0.5f);
        MySprite labelSprite = MySprite.CreateText("", FONT, _textColor, scaledFontSize);

        // Draw major ticks
        for (int angle = 0; angle < 180; angle += MAJOR_TICK_INTERVAL)
        {
            float rotation = (float)MathHelper.ToRadians(angle + angleOffset);
            if (angle < 180)
            {
                line.RotationOrScale = rotation;
                frame.Add(line);
            }
        }

        // Clip center of major ticks
        circleClip.Size = scale * RADIAL_COMPASS_MAJOR_CLIP_SIZE;
        frame.Add(circleClip);

        // Draw labels
        for (int angle = 0; angle < 360; angle += MAJOR_TICK_INTERVAL)
        {
            if (angle % 90 != 0)
                continue;

            float rotation = (float)MathHelper.ToRadians(angle + angleOffset);
            string label = "";
            _cardinalDirectionDict.TryGetValue(angle, out label);
            Vector2 labelOffset = new Vector2(scale * RADIAL_COMPASS_LABEL_RADIUS * MyMath.FastSin(rotation), -scale * RADIAL_COMPASS_LABEL_RADIUS * MyMath.FastCos(rotation));
            labelSprite.Data = label;
            labelSprite.Position = screenCenter + labelOffset + fontVertOffset;
            frame.Add(labelSprite);
        }

        // Add pip indicator
        MySprite pipSprite = MySprite.CreateSprite("Triangle", screenCenter + scale * RADIAL_COMPASS_PIP_LOCATION, scale * PIP_SIZE);
        pipSprite.Color = _pipColor;
        frame.Add(pipSprite);

        if (_drawBearing)
        {
            // Draw angle text box
            Vector2 textBoxPos = screenCenter + fontVertOffset;
            Vector2 textBoxSize = scale * TEXT_BOX_SIZE;
            Vector2 textHorizOffset = TEXT_BOX_HORIZ_SPACING * scale;
            Vector2 textBoxCenter = screenCenter;
            // background
            MySprite textBox = MySprite.CreateSprite("SquareSimple", textBoxCenter, textBoxSize);
            textBox.Color = _backgroundColor;
            frame.Add(textBox);
            // box
            textBox.Data = "AH_TextBox";
            textBox.Color = _textBoxColor;
            frame.Add(textBox);

            // Write digits
            string bearingStr = $"{Bearing:000}";
            // hundreds
            MySprite digit = MySprite.CreateText(bearingStr.Substring(0, 1), FONT, _textColor, FONT_SIZE * scale);
            digit.Position = textBoxPos - textHorizOffset;
            frame.Add(digit);
            // tens
            digit.Data = bearingStr.Substring(1, 1);
            digit.Position = textBoxPos;
            frame.Add(digit);
            // ones
            digit.Data = bearingStr.Substring(2, 1);
            digit.Position = textBoxPos + textHorizOffset;
            frame.Add(digit);
        }
    }

    int GetBearingAngle(int angle)
    {
        if (angle < 0)
        {
            return angle + 360;
        }

        if (angle > 360)
        {
            return angle - 360;
        }
        return angle;
    }
}